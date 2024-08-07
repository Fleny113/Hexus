using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
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
        logController.Semaphore.Wait();

        try
        {
            File.AppendAllText(
                $"{EnvironmentHelper.ApplicationLogsDirectory}/{application.Name}.log",
                $"[{applicationLog.Date.DateTime:O},{applicationLog.LogType}] {applicationLog.Text}\n"
            );
        }
        finally
        {
            logController.Semaphore.Release();
        }
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
            var logs = GetLogsFromFile(application, logController, lines, currentExecution, before, after);

            foreach (var log in logs.Reverse())
            {
                if (!IsLogDateInRange(log.Date, before, after)) continue;

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
        _logControllers[application.Name] = new LogController();
    }

    public bool UnregisterApplication(HexusApplication application)
    {
        LogUnRegisteringApplication(logger, application.Name);
        return _logControllers.Remove(application.Name, out _);
    }

    public void DeleteApplication(HexusApplication application)
    {
        UnregisterApplication(application);
        File.Delete($"{EnvironmentHelper.ApplicationLogsDirectory}/{application.Name}.log");
    }

    #region Log From File Parser

    private IEnumerable<ApplicationLog> GetLogsFromFile(HexusApplication application, LogController logController,
        int lines, bool currentExecution, DateTimeOffset? before, DateTimeOffset? after)
    {
        logController.Semaphore.Wait();

        try
        {
            using var stream = File.OpenRead($"{EnvironmentHelper.ApplicationLogsDirectory}/{application.Name}.log");
            using var reader = new StreamReader(stream, Encoding.UTF8);

            if (stream.Length <= 2)
                yield break;

            var lineFound = 0;

            // Go to the end of the file.
            stream.Position = stream.Length - 1;

            // If the last character is a LF we can skip it as it isn't a log line.
            if (stream.ReadByte() == '\n')
                stream.Position -= 2;

            while (lineFound < lines)
            {
                // We are in a line, so we go back until we find the lastNewline of this line.
                if (stream.Position != 0 && stream.ReadByte() != '\n')
                {
                    if (stream.Position >= 2)
                    {
                        stream.Position -= 2;
                        continue;
                    }

                    break;
                }

                var positionBeforeRead = stream.Position;

                reader.DiscardBufferedData();
                var line = reader.ReadLine();

                stream.Position = positionBeforeRead;

                if (string.IsNullOrEmpty(line))
                {
                    if (stream.Position >= 2)
                        stream.Position -= 2;

                    continue;
                }

                var startMetadata = line.IndexOf('[') + 1;
                var endDate = line.IndexOf(',', startMetadata);
                var endMetadata = line.IndexOf(']', endDate);
                var startMessage = line.IndexOf(' ', endMetadata) + 1;

                var logDateString = line[startMetadata..endDate];
                if (!TryLogTimeFormat(logDateString, out var logDate))
                {
                    LogFailedDateTimeParsing(logger, application.Name, logDateString);

                    if (stream.Position >= 2)
                    {
                        stream.Position -= 2;
                        continue;
                    }

                    break;
                }

                if (!IsLogDateInRange(logDate, before, after))
                {
                    if (stream.Position >= 2)
                    {
                        stream.Position -= 2;
                        continue;
                    }

                    break;
                }

                lineFound++;

                var logTypeString = line[(endDate + 1)..endMetadata];
                var logText = line[startMessage..];

                if (!Enum.TryParse<LogType>(logTypeString.AsSpan(), out var logType))
                {
                    LogFailedTypeParsing(logger, application.Name, logTypeString);

                    if (stream.Position >= 2)
                    {
                        stream.Position -= 2;
                        continue;
                    }

                    break;
                }

                yield return new ApplicationLog(logDate, logType, logText);

                // We only wanted the current execution and we found an application started notice. We should now stop.
                if (currentExecution && logType == LogType.SYSTEM && logText == ApplicationStartedLog)
                {
                    yield break;
                }

                if (stream.Position >= 2)
                {
                    stream.Position -= 2;
                    continue;
                }

                break;
            }
        }
        finally
        {
            logController.Semaphore.Release();
        }
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

    [LoggerMessage(LogLevel.Warning, "Unable to get log controller for application \"{Name}\"")]
    private static partial void LogUnableToGetLogController(ILogger logger, string name);

    [LoggerMessage(LogLevel.Debug, "Application \"{Name}\" is being registered in the process logs service ")]
    private static partial void LogRegisteringApplication(ILogger logger, string name);

    [LoggerMessage(LogLevel.Debug, "Application \"{Name}\" is being unregistered in the process logs service ")]
    private static partial void LogUnRegisteringApplication(ILogger logger, string name);

    [LoggerMessage(LogLevel.Trace, "Application \"{Name}\" says: '{OutputData}'")]
    private static partial void LogApplicationOutput(ILogger logger, string name, string outputData);

    private record LogController
    {
        public SemaphoreSlim Semaphore { get; } = new(initialCount: 1, maxCount: 1);
        public List<Channel<ApplicationLog>> Channels { get; } = [];
    }
}
