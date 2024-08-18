using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace Hexus.Daemon.Services;

internal partial class ProcessLogsService(ILogger<ProcessLogsService> logger)
{
    internal const string ApplicationStartedLog = "-- Application started --";
    internal static readonly CompositeFormat ApplicationStoppedLog = CompositeFormat.Parse("-- Application stopped [Exit code: {0}] --");

    private readonly Dictionary<string, LogController> _logControllers = [];

    internal void ProcessApplicationLog(HexusApplication application, LogType logType, string message)
    {
        if (!_logControllers.TryGetValue(application.Name, out var logController))
        {
            LogUnableToGetLogController(logger, application.Name);
            return;
        }

        if (logType != LogType.SYSTEM)
        {
            LogApplicationOutput(logger, application.Name, message);
        }

        var applicationLog = new ApplicationLog(DateTimeOffset.UtcNow, logType, message);

        logController.Channels.ForEach(channel => channel.Writer.TryWrite(applicationLog));
        logController.FileWriter.WriteLine($"[{applicationLog.Date.DateTime:O},{applicationLog.LogType}] {applicationLog.Text}");
    }

    public async IAsyncEnumerable<ApplicationLog> GetLogs(HexusApplication application, int lines, bool streaming,
        bool currentExecution, DateTimeOffset? before, DateTimeOffset? after, [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_logControllers.TryGetValue(application.Name, out var logController))
        {
            LogUnableToGetLogController(logger, application.Name);
            yield break;
        }

        Channel<ApplicationLog>? channel = null;

        if (streaming)
        {
            channel = Channel.CreateUnbounded<ApplicationLog>();
            logController.Channels.Add(channel);
        }

        try
        {
            var logs = GetLogsFromFileAsync(application, lines, currentExecution, before, after, ct);
            
            await foreach (var log in logs.Reverse())
            {
                yield return log;
            }

            if (!streaming || channel is null)
                yield break;

            await foreach (var log in channel.Reader.ReadAllAsync(ct))
            {
                if (!IsLogDateInRange(log.Date, before, after)) continue;

                yield return log;
            }
        }
        finally
        {
            if (channel is not null)
            {
                channel.Writer.Complete();
                logController.Channels.Remove(channel);
            }
        }
    }

    public void RegisterApplication(HexusApplication application)
    {
        LogRegisteringApplication(logger, application.Name);

        var fileStreamOptions = new FileStreamOptions()
        {
            Access = FileAccess.Write,
            Mode = FileMode.Append,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        };

        _logControllers[application.Name] = new LogController
        {
            FileWriter = new StreamWriter($"{EnvironmentHelper.ApplicationLogsDirectory}/{application.Name}.log", Encoding.UTF8, fileStreamOptions)
            {
                AutoFlush = true,
                NewLine = "\n",
            }
        };
    }

    public bool UnregisterApplication(HexusApplication application)
    {
        LogUnregisteringApplication(logger, application.Name);
        return _logControllers.Remove(application.Name, out _);
    }

    public void DeleteApplication(HexusApplication application)
    {
        UnregisterApplication(application);
        File.Delete($"{EnvironmentHelper.ApplicationLogsDirectory}/{application.Name}.log");
    }

    #region Log From File Parser

    private const int _readBufferSize = 4096;

    private async IAsyncEnumerable<ApplicationLog> GetLogsFromFileAsync(HexusApplication application, int lines, bool currentExecution,
        DateTimeOffset? before, DateTimeOffset? after, [EnumeratorCancellation] CancellationToken ct)
    {
        var options = new FileStreamOptions
        {
            Access = FileAccess.Read,
            Mode = FileMode.Open,
            Share = FileShare.ReadWrite,
        };

        using var file = File.Open($"{EnvironmentHelper.ApplicationLogsDirectory}/{application.Name}.log", options);

        // 36 is the size of the start of a line in the logs
        if (file.Length <= 36) 
        {
            yield break;
        }

        using var memPoll = MemoryPool<byte>.Shared.Rent(_readBufferSize);
        var sb = new StringBuilder();
        var lineCount = 0;

        // Go to the end
        file.Position = Math.Max(file.Length - _readBufferSize, 0);

        // The buffer may need to be resized to avoid duplicating some data
        var actualBufferSize = _readBufferSize;

        var bytesRead = await file.ReadAsync(memPoll.Memory[0..actualBufferSize], ct);

        // We only want to consume the memory that has data we have just read
        var readMemory = memPoll.Memory[0..bytesRead];

        // We don't know where new lines are, we will search them but we need to start from the end,
        // but we know the last line (of the file) should always be a new line so we can already skip that
        var lastNewLine = bytesRead - 1;

        // If the position of the cursor is the same as the size of our (actual) buffer size it means we started reading from 0
        var isEntireStream = file.Position == actualBufferSize;

        while (lineCount < lines)
        {
            var pos = readMemory.Span[0..lastNewLine].LastIndexOf("\n"u8);

            var line = Encoding.UTF8.GetString(readMemory.Span[(pos + 1)..lastNewLine]);
            lastNewLine = pos;

            if (!isEntireStream && pos == -1)
            {
                // We need to store this part of the log line or else we are going to lose it
                sb.Insert(0, line);

                // We don't want to read stuff twice, so we need "resize" our buffer
                actualBufferSize = (int)Math.Min(file.Position - bytesRead, _readBufferSize);

                // Go back to before the read, and get another buffer of space.
                file.Position = Math.Max(file.Position - bytesRead - _readBufferSize, 0);

                bytesRead = await file.ReadAsync(memPoll.Memory[0..actualBufferSize], ct);

                readMemory = memPoll.Memory[0..bytesRead];
                lastNewLine = bytesRead;
                isEntireStream = file.Position == actualBufferSize;

                continue;
            }

            // We have buffered a string, use it and clear the StringBuilder to be reused
            if (sb.Length > 0)
            {
                sb.Insert(0, line);
                line = sb.ToString();

                sb.Clear();
            }

            if (!TryParseLogLine(line, application, out var appLog))
            {
                continue;
            }

            if (!IsLogDateInRange(appLog.Date, before, after))
            {
                continue;
            }

            lineCount++;
            yield return appLog;

            // We only wanted the current execution and we found an application started notice. We should now stop.
            if (currentExecution && appLog.LogType == LogType.SYSTEM && appLog.Text == ApplicationStartedLog)
            {
                break;
            }
        }
    }

    private bool TryParseLogLine(ReadOnlySpan<char> logSpan, HexusApplication application, [MaybeNullWhen(false)] out ApplicationLog appLog)
    {
        appLog = null;

        if (logSpan[0] != '[')
        {
            LogFailedFormatChecks(logger, application.Name);
            return false;
        }

        var endDate = logSpan.IndexOf(',');
        if (endDate == -1)
        {
            LogFailedFormatChecks(logger, application.Name);
            return false;
        }

        var endMetadata = logSpan.IndexOf(']');
        if (endMetadata == -1)
        {
            LogFailedFormatChecks(logger, application.Name);
            return false;
        }

        var startMessage = endMetadata + 2;

        var dateSpan = logSpan[1..endDate];

        if (!TryLogTimeFormat(dateSpan, out var date))
        {
            LogFailedDateTimeParsing(logger, application.Name, dateSpan.ToString());
            return false;
        }

        var logTypeSpan = logSpan[(endDate + 1)..endMetadata];
        var logText = logSpan[startMessage..];

        if (!Enum.TryParse<LogType>(logTypeSpan, out var logType))
        {
            LogFailedTypeParsing(logger, application.Name, logTypeSpan.ToString());
            return false;
        }

        appLog = new ApplicationLog(date, logType, logText.ToString());
        return true;
    }

    private static bool TryLogTimeFormat(ReadOnlySpan<char> logDate, out DateTimeOffset dateTimeOffset)
    {
        return DateTimeOffset.TryParseExact(logDate, "O", null, DateTimeStyles.AssumeUniversal, out dateTimeOffset);
    }

    private static bool IsLogDateInRange(DateTimeOffset time, DateTimeOffset? before = null, DateTimeOffset? after = null)
    {
        if (before is not null && time > before.Value)
            return false;

        if (after is not null && time < after.Value)
            return false;

        return true;
    }

    #endregion

    [LoggerMessage(LogLevel.Warning, "There was an error parsing the log file for application {Name}: Couldn't parse \"{LogDate}\" as a DateTime. Skipping log line.")]
    private static partial void LogFailedDateTimeParsing(ILogger logger, string name, string logDate);

    [LoggerMessage(LogLevel.Warning, "There was an error parsing the log file for application {Name}: Couldn't parse \"{LogType}\" as a LogType. Skipping log line.")]
    private static partial void LogFailedTypeParsing(ILogger logger, string name, string logType);
    [LoggerMessage(LogLevel.Warning, "There was an error parsing the log file for application {Name}: Couldn't parse line. Invalid format")]
    private static partial void LogFailedFormatChecks(ILogger logger, string name);

    [LoggerMessage(LogLevel.Warning, "Unable to get log controller for application \"{Name}\"")]
    private static partial void LogUnableToGetLogController(ILogger logger, string name);

    [LoggerMessage(LogLevel.Debug, "Application \"{Name}\" is being registered in the process logs service ")]
    private static partial void LogRegisteringApplication(ILogger logger, string name);

    [LoggerMessage(LogLevel.Debug, "Application \"{Name}\" is being unregistered in the process logs service ")]
    private static partial void LogUnregisteringApplication(ILogger logger, string name);

    [LoggerMessage(LogLevel.Trace, "Application \"{Name}\" says: '{OutputData}'")]
    private static partial void LogApplicationOutput(ILogger logger, string name, string outputData);

    private record LogController
    {
        public required StreamWriter FileWriter { get; init; }
        public List<Channel<ApplicationLog>> Channels { get; } = [];
    }
}
