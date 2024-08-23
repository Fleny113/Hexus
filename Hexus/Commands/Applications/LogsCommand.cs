using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Spectre.Console;
using System.Buffers;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;

namespace Hexus.Commands.Applications;

internal static class LogsCommand
{
    private static readonly Argument<string> NameArgument = new("name")
    {
        Description = "The name of the application",
    };
    private static readonly Option<int?> LinesOption = new(["-l", "--lines"])
    {
        Description = "The number of lines to show from the log file",
    };
    private static readonly Option<bool> DontStream = new("--no-streaming")
    {
        Description = "Disable the streaming of new logs. It will only fetch from the log file",
    };
    private static readonly Option<bool> DontShowDates = new("--no-dates")
    {
        Description = "Disable the dates of the log lines. Useful if you already have those in your log file",
    };
    private static readonly Option<bool> CurrentExecution = new(["-c", "--current"])
    {
        Description = "Show logs only from the current or last execution",
    };
    private static readonly Option<DateTime?> ShowLogsAfter = new(["-a", "--after"])
    {
        Description = "Show logs only after a specified date. The date is in the same timezone provided by the \"timezone\" option",
    };
    private static readonly Option<DateTime?> ShowLogsBefore = new(["-b", "--before"])
    {
        Description = "Show logs only before a specified date. The date is in the same timezone provided by the \"timezone\" option",
    };
    private static readonly Option<string> TimezoneOption = new(["-t", "--timezone"])
    {
        Description = "Show the log dates in a specified timezone. The timezone should be compatible with the one provided by your system.",
    };

    public static readonly Command Command = new("logs", "View the logs of an application")
    {
        NameArgument,
        LinesOption,
        DontStream,
        DontShowDates,
        CurrentExecution,
        ShowLogsAfter,
        ShowLogsBefore,
        TimezoneOption,
    };

    static LogsCommand()
    {
        TimezoneOption.AddValidator(result =>
        {
            var timezone = result.GetValueForOption(TimezoneOption);

            if (timezone is null) return;
            if (TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _)) return;

            result.ErrorMessage = $"The TimeZone was not found on the local computer: {timezone}";
        });

        TimezoneOption.SetDefaultValue(TimeZoneInfo.Local.Id);

        Command.AddAlias("log");
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var lines = context.ParseResult.GetValueForOption(LinesOption) ?? 100;
        var noStreaming = context.ParseResult.GetValueForOption(DontStream);
        var noDates = context.ParseResult.GetValueForOption(DontShowDates);
        var currentExecution = context.ParseResult.GetValueForOption(CurrentExecution);
        var showAfter = context.ParseResult.GetValueForOption(ShowLogsAfter);
        var showBefore = context.ParseResult.GetValueForOption(ShowLogsBefore);
        var timezoneOption = context.ParseResult.GetValueForOption(TimezoneOption);
        var ct = context.GetCancellationToken();

        ArgumentNullException.ThrowIfNull(timezoneOption);
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timezoneOption);

        var localizedBefore = showBefore is { } before ? TimeZoneInfo.ConvertTime(before, timeZoneInfo) : (DateTime?)null;
        var localizedAfter = showAfter is { } after ? TimeZoneInfo.ConvertTime(after, timeZoneInfo) : (DateTime?)null;

        // Show the log file

        var logFileName = $"{EnvironmentHelper.ApplicationLogsDirectory}/{name}.log";

        if (!File.Exists(logFileName))
        {
            PrettyConsole.Error.MarkupLine("The request [indianred1]application does not have a log file[/]. Does the application exist?");
            context.ExitCode = 1;
            return;
        }

        var fileLogs = GetLogsFromFileAsync(logFileName, lines, currentExecution, localizedBefore, localizedAfter, ct);

        await foreach (var log in fileLogs.Reverse())
        {
            PrintLogLine(log, timeZoneInfo, !noDates);
        }

        // Stream the new logs

        if (noStreaming) return;

        // It doesn't make sense to stream stuff in the past.
        if (showBefore < DateTimeOffset.UtcNow) return;

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Out.MarkupLine("The [indianred1]daemon is not running[/]. Logs will not be streamed.");
            return;
        }

        var showBeforeParam = showBefore is not null ? $"&before={localizedBefore:O}" : null;
        var showAfterParam = showAfter is not null ? $"&after={localizedAfter:O}" : null;

        try
        {
            var logsRequest = await HttpInvocation.HttpClient.GetAsync(
                $"/{name}/logs?{showBeforeParam}{showAfterParam}",
                HttpCompletionOption.ResponseHeadersRead,
                ct
            );

            if (!logsRequest.IsSuccessStatusCode)
            {
                await HttpInvocation.HandleFailedHttpRequestLogging(logsRequest, ct);
                context.ExitCode = 1;
                return;
            }


            var logs = logsRequest.Content.ReadFromJsonAsAsyncEnumerable<ApplicationLog>(HttpInvocation.JsonSerializerOptions, ct);

            await foreach (var logLine in logs)
            {
                if (logLine is null) continue;

                PrintLogLine(logLine, timeZoneInfo, !noDates);
            }
        }
        catch (TaskCanceledException)
        {
            // Discard the exception
        }
    }

    private static void PrintLogLine(ApplicationLog log, TimeZoneInfo timeZoneInfo, bool showDates)
    {
        var color = GetLogTypeColor(log.LogType);

        var timezone = TimeZoneInfo.ConvertTime(log.Date, timeZoneInfo);
        var date = showDates ? $"{timezone:yyyy-MM-dd HH:mm:ss} [{color}]| " : $"[{color}]";
        var text = log.Text.EscapeMarkup();

        PrettyConsole.OutLimitlessWidth.MarkupLine($"{date}{log.LogType} |[/] {text}");
    }

    private static Color GetLogTypeColor(LogType logType) => logType switch
    {
        LogType.STDOUT => Color.SpringGreen3,
        LogType.STDERR => Color.Red3_1,
        LogType.SYSTEM => Color.MediumPurple2,
        _ => throw new ArgumentOutOfRangeException(nameof(logType), "The requested log type is not mapped to a color"),
    };

    #region Log From File Parser

    private const int _readBufferSize = 4096;

    private static async IAsyncEnumerable<ApplicationLog> GetLogsFromFileAsync(string fileName, int lines, bool currentExecution,
        DateTimeOffset? before, DateTimeOffset? after, [EnumeratorCancellation] CancellationToken ct)
    {
        // While we allow others to write to this file, the expectation is that they will only append. We cannot enforce that sadly.
        using var file = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

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

            if (!TryParseLogLine(line, out var appLog))
            {
                continue;
            }

            if (!appLog.IsLogDateInRange(before, after))
            {
                continue;
            }

            lineCount++;
            yield return appLog;

            // We only wanted the current execution and we found an application started notice. We should now stop.
            if (currentExecution && appLog.LogType == LogType.SYSTEM && appLog.Text == ProcessLogsService.ApplicationStartedLog)
            {
                break;
            }

            if (isEntireStream && pos == -1)
            {
                break;
            }
        }
    }

    private static bool TryParseLogLine(ReadOnlySpan<char> logSpan, [MaybeNullWhen(false)] out ApplicationLog appLog)
    {
        appLog = null;

        if (logSpan[0] != '[')
        {
            return false;
        }

        var endDate = logSpan.IndexOf(',');
        if (endDate == -1)
        {
            return false;
        }

        var endMetadata = logSpan.IndexOf(']');
        if (endMetadata == -1)
        {
            return false;
        }

        var startMessage = endMetadata + 2;

        var dateSpan = logSpan[1..endDate];

        if (!TryLogTimeFormat(dateSpan, out var date))
        {
            return false;
        }

        var logTypeSpan = logSpan[(endDate + 1)..endMetadata];
        var logText = logSpan[startMessage..];

        if (!Enum.TryParse<LogType>(logTypeSpan, out var logType))
        {
            return false;
        }

        appLog = new ApplicationLog(date, logType, logText.ToString());
        return true;
    }

    private static bool TryLogTimeFormat(ReadOnlySpan<char> logDate, out DateTimeOffset dateTimeOffset)
    {
        return DateTimeOffset.TryParseExact(logDate, "O", null, DateTimeStyles.AssumeUniversal, out dateTimeOffset);
    }

    #endregion

}
