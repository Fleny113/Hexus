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

    #region Behaviour options

    private static readonly Option<bool> DontStream = new("--no-streaming")
    {
        Description = "Disable the streaming of new logs. It will only fetch from the log file",
    };

    #endregion
    #region Display options

    private static readonly Option<bool> DontShowDates = new("--no-dates")
    {
        Description = "Disable the dates of the log lines. Useful if you already have those in your log file",
    };
    private static readonly Option<string> TimezoneOption = new(["-t", "--timezone"])
    {
        Description = "Show the log dates in a specified timezone. The timezone should be compatible with the one provided by your system.",
    };

    #endregion
    #region Filtering options

    private static readonly Option<int> LinesOption = new(["-l", "--lines"])
    {
        Description = "The number of lines to display",
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

    #endregion

    public static readonly Command Command = new("logs", "View the logs of an application")
    {
        NameArgument,
        DontStream,
        DontShowDates,
        TimezoneOption,
        LinesOption,
        CurrentExecution,
        ShowLogsAfter,
        ShowLogsBefore,
    };

    static LogsCommand()
    {
        TimezoneOption.SetDefaultValue(TimeZoneInfo.Local.Id);
        TimezoneOption.AddValidator(result =>
        {
            var timezone = result.GetValueForOption(TimezoneOption);

            if (timezone is null) return;
            if (TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _)) return;

            result.ErrorMessage = $"The TimeZone was not found on the local computer: {timezone}";
        });

        LinesOption.SetDefaultValue(100);
        LinesOption.AddValidator(result =>
        {
            var lines = result.GetValueForOption(LinesOption);

            if (lines is >= 0 or -1) return;

            result.ErrorMessage = "The number of lines should be more or equal to 0 or -1 to disable the limit.";
        });

        Command.AddAlias("log");
        Command.SetHandler(Handler);
    }

    private static async Task<int> Handler(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var streaming = !context.ParseResult.GetValueForOption(DontStream);

        var linesOption = context.ParseResult.GetValueForOption(LinesOption);
        var noDates = context.ParseResult.GetValueForOption(DontShowDates);
        var timezoneOption = context.ParseResult.GetValueForOption(TimezoneOption);

        var currentExecution = context.ParseResult.GetValueForOption(CurrentExecution);
        var showAfter = context.ParseResult.GetValueForOption(ShowLogsAfter);
        var showBefore = context.ParseResult.GetValueForOption(ShowLogsBefore);

        var ct = context.GetCancellationToken();

        // Time zone validation
        ArgumentNullException.ThrowIfNull(timezoneOption);
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timezoneOption);

        // Convert the before/after from the timezone specified to UTC to be used to filter logs
        //  the dates in the log file will then be converted back to the timezone provided
        DateTime? utcBefore = showBefore is { } before ? TimeZoneInfo.ConvertTimeToUtc(before, timeZoneInfo) : null;
        DateTime? utcAfter = showAfter is { } after ? TimeZoneInfo.ConvertTimeToUtc(after, timeZoneInfo) : null;

        var lines = linesOption == -1 ? int.MaxValue : linesOption;

        var logFileName = $"{EnvironmentHelper.ApplicationLogsDirectory}/{name}.log";

        if (!File.Exists(logFileName))
        {
            PrettyConsole.Error.MarkupLine("The request [indianred1]application does not have a log file[/]. Does the application exist?");
            return 1;
        }

        // Streaming check for the daemon status
        if (streaming)
        {
            var isDaemonRunning = await HttpInvocation.CheckForRunningDaemon(ct);
            streaming = isDaemonRunning;

            if (!isDaemonRunning)
            {
                PrettyConsole.Out.MarkupLine("""
                    [yellow1]Warning[/]: Streaming was enabled, but the [indianred1]daemon is not running[/].

                    [italic]Press any key to continue.[/]
                    [italic gray]To disable this warning when the daemon is not running, use the "--no-streaming" flag[/]
                    """);

                Console.ReadKey(intercept: true);
            }
        }

        var streamedLogs = streaming ? await CreateStreamingLogs(name, utcBefore, ct) : null;
        var logFileLogs = GetLogsFromFileAsync(logFileName, lines, currentExecution, utcBefore, utcAfter, ct).Reverse();

        var logs = streamedLogs is not null
            ? logFileLogs.Concat(streamedLogs).Where(x => x is not null).Select(x => x!)
            : logFileLogs;

        try
        {
            await foreach (var log in logs.WithCancellation(ct))
            {
                PrettyConsole.OutLimitlessWidth.MarkupLine(GetLogLine(log, timeZoneInfo, !noDates));
            }
        }
        catch (TaskCanceledException)
        {
            // Silently ignore this exception.
        }

        return 0;
    }

    private static string GetLogLine(ApplicationLog log, TimeZoneInfo timeZoneInfo, bool showDates)
    {
        var color = GetLogTypeColor(log.LogType);

        var timezone = TimeZoneInfo.ConvertTime(log.Date, timeZoneInfo);
        var date = showDates ? $"{timezone:yyyy-MM-dd HH:mm:ss} [{color}]| " : $"[{color}]";

        var header = $"{date}{log.LogType} |[/] ";

        return $"{header}{log.Text.EscapeMarkup()}";
    }

    private static Color GetLogTypeColor(LogType logType) => logType switch
    {
        LogType.STDOUT => Color.SpringGreen3,
        LogType.STDERR => Color.Red3_1,
        LogType.SYSTEM => Color.MediumPurple2,
        _ => throw new ArgumentOutOfRangeException(nameof(logType), "The requested log type is not mapped to a color"),
    };

    #region Log From File Parser

    private const int ReadBufferSize = 4096;

    private static async IAsyncEnumerable<ApplicationLog> GetLogsFromFileAsync(string fileName, int lines, bool currentExecution,
        DateTimeOffset? before, DateTimeOffset? after, [EnumeratorCancellation] CancellationToken ct)
    {
        await using var file = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        using var memPoll = MemoryPool<byte>.Shared.Rent(ReadBufferSize);
        var sb = new StringBuilder();
        var lineCount = 0;

        file.Position = Math.Max(file.Length - ReadBufferSize, 0);

        // The buffer may need to be resized to avoid duplicating some data
        var actualBufferSize = (int)Math.Min(ReadBufferSize, file.Length);

        var bytesRead = await file.ReadAsync(memPoll.Memory[..actualBufferSize], ct);

        // We only want to consume the memory that has data we have just read to avoid some work
        var readMemory = memPoll.Memory[..bytesRead];

        // The position of the last \n we found. Right after a read this will be the end of the buffer
        var lastNewLine = bytesRead;

        // If the position of the cursor is the same as the size of our (actual) buffer size it means we started reading from 0
        var isEntireStream = file.Position - bytesRead == 0;

        // This is a value used when a line is between chunks, used to store where the line starts, relative to the file.
        long lineOffset = -1;

        while (lineCount < lines)
        {
            // If we have read the beginning of the file, we can stop now.
            // We are sure that there aren't new chunks since the previous interaction would have read a new chunk if that was the case.
            if (lastNewLine == -1)
            {
                break;
            }

            var pos = readMemory.Span[..lastNewLine].LastIndexOf("\n"u8);

            var line = Encoding.UTF8.GetString(readMemory.Span[(pos + 1)..lastNewLine]);
            lastNewLine = pos;

            // If this isn't the entire stream, and we did not found a \n, we need to fetch another chunk
            if (!isEntireStream && pos == -1)
            {
                // Save the start of this line, (if there isn't a line already saved)
                if (lineOffset == -1) lineOffset = file.Position - bytesRead + lastNewLine + 1;

                // We need to store this part of the line or else we are going to lose it
                sb.Insert(0, line);

                // We don't want to read stuff twice, so we need "resize" our buffer
                actualBufferSize = (int)Math.Min(file.Position - bytesRead, ReadBufferSize);

                // Go back to before the read, and get another buffer of space.
                file.Position = Math.Max(file.Position - bytesRead - ReadBufferSize, 0);

                bytesRead = await file.ReadAsync(memPoll.Memory[..actualBufferSize], ct);

                readMemory = memPoll.Memory[..bytesRead];
                lastNewLine = bytesRead;

                isEntireStream = file.Position - bytesRead == 0;

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

            // If the line is after the date we want, we want to ignore this one and continue searching.
            if (before is { } b && appLog.Date > b)
            {
                continue;
            }

            // If the line is before the date we want, we can stop now.
            // Since the line file is sorted oldest to newest after we find a line that is before the after filter all the next lines will be before the after as well.
            if (after is { } a && appLog.Date < a)
            {
                break;
            }

            lineCount++;

            // lineOffset is -1 when the line is not between chunks.
            yield return appLog;

            lineOffset = -1;

            // We only wanted the current execution, and we found an application started notice. We should now stop.
            if (currentExecution && appLog is { LogType: LogType.SYSTEM, Text: ProcessLogsService.ApplicationStartedLog })
            {
                break;
            }
        }
    }

    private static bool TryParseLogLine(ReadOnlySpan<char> logSpan, [MaybeNullWhen(false)] out ApplicationLog appLog)
    {
        appLog = null;

        if (logSpan.Length == 0 || logSpan[0] != '[')
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

    private static async Task<IAsyncEnumerable<ApplicationLog?>?> CreateStreamingLogs(string name, DateTime? before, CancellationToken ct)
    {
        var showBeforeParam = before is not null ? $"before={before:O}" : null;

        var logsRequest = await HttpInvocation.HttpClient.GetAsync(
            $"/{name}/logs?{showBeforeParam}",
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );

        if (logsRequest.IsSuccessStatusCode)
        {
            return logsRequest.Content.ReadFromJsonAsAsyncEnumerable<ApplicationLog>(HttpInvocation.JsonSerializerContext.ApplicationLog, ct);
        }

        PrettyConsole.Out.MarkupLine("""
                                     [yellow1]Warning[/]: Streaming was enabled, but the [indianred1]daemon returned an error[/].

                                     [italic]Press any key to continue.[/]
                                     [italic gray]See below for more details:[/]
                                     """);

        await HttpInvocation.HandleFailedHttpRequestLogging(logsRequest, ct);
        Console.ReadKey(intercept: true);

        return null;
    }
}
