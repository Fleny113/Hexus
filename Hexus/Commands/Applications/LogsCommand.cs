using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Spectre.Console;
using System.Buffers;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Hexus.Commands.Applications;

internal static class LogsCommand
{
    private static readonly Argument<string> NameArgument = new("name")
    {
        Description = "The name of the application",
    };

    // Behaviour options

    private static readonly Option<bool> DontStream = new("--no-streaming")
    {
        Description = "Disable the streaming of new logs. It will only fetch from the log file",
    };

    // Display options

    private static readonly Option<bool> DontShowDates = new("--no-dates")
    {
        Description = "Disable the dates of the log lines. Useful if you already have those in your log file",
    };
    private static readonly Option<string> TimezoneOption = new(["-t", "--timezone"])
    {
        Description = "Show the log dates in a specified timezone. The timezone should be compatible with the one provided by your system.",
    };

    // Filtering options

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

    public static readonly Command Command = new("logs", "View the logs of an application")
    {
        NameArgument,
        DontStream,
        DontShowDates,
        TimezoneOption,
        CurrentExecution,
        ShowLogsAfter,
        ShowLogsBefore,
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
        var streaming = !context.ParseResult.GetValueForOption(DontStream);

        var noDates = context.ParseResult.GetValueForOption(DontShowDates);
        var timezoneOption = context.ParseResult.GetValueForOption(TimezoneOption);

        var currentExecution = context.ParseResult.GetValueForOption(CurrentExecution);
        var showAfter = context.ParseResult.GetValueForOption(ShowLogsAfter);
        var showBefore = context.ParseResult.GetValueForOption(ShowLogsBefore);

        var ct = context.GetCancellationToken();

        // Time zone validation
        ArgumentNullException.ThrowIfNull(timezoneOption);
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timezoneOption);

        // Adjust to timezone the before and the after
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

                Console.ReadKey(true);
            }
        }

        try
        {
            // \e[?1049h is the escape sequence to open an alternate screen buffer.
            PrettyConsole.Out.Write("\e[?1049h");
            Console.SetCursorPosition(0, 0);

            await Handler(logFileName, currentExecution, timeZoneInfo, localizedBefore, localizedAfter, !noDates, streaming, ct);
        }
        catch (TaskCanceledException)
        {
            // We really don't care for a TaskCancelledException
            return;
        }
        finally
        {
            // \e[?1049l is the escape sequence to close the alternate screen buffer.
            PrettyConsole.Out.Write("\e[?1049l");
        }

        // Stream the new logs

        //if (!streaming) return;

        //// It doesn't make sense to stream stuff in the past.
        //if (showBefore < DateTimeOffset.UtcNow) return;

        //var showBeforeParam = showBefore is not null ? $"&before={localizedBefore:O}" : null;
        //var showAfterParam = showAfter is not null ? $"&after={localizedAfter:O}" : null;

        //var logsRequest = await HttpInvocation.HttpClient.GetAsync(
        //    $"/{name}/logs?{showBeforeParam}{showAfterParam}",
        //    HttpCompletionOption.ResponseHeadersRead,
        //    ct
        //);

        //if (!logsRequest.IsSuccessStatusCode)
        //{
        //    await HttpInvocation.HandleFailedHttpRequestLogging(logsRequest, ct);
        //    context.ExitCode = 1;
        //    return;
        //}


        //var logs = logsRequest.Content.ReadFromJsonAsAsyncEnumerable<ApplicationLog>(HttpInvocation.JsonSerializerOptions, ct);

        //await foreach (var logLine in logs)
        //{
        //    if (logLine is null) continue;

        //    PrintLogLine(logLine, timeZoneInfo, !noDates);
        //}
    }

    private static async Task Handler(string log, bool current, TimeZoneInfo timezone, DateTimeOffset? before,
        DateTimeOffset? after, bool dates, bool streaming, CancellationToken ct)
    {
        // We need to show the initial file.

        // While we allow others to write to this file, the expectation is that they will only append. We cannot enforce that sadly.
        using var file = File.Open(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var offset = file.Length;
        var lines = Console.WindowHeight - 1;

        var logs = await GetLogsFromFileBackwardsAsync(file, lines, offset, current, before, after, ct).Reverse().ToListAsync(ct);

        ReprintScreen(lines, logs, timezone, dates);

        // From now on we will keep track of the user input and react to it accordingly

        while (!ct.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.Q:
                    case ConsoleKey.Escape:
                        return;
                    case ConsoleKey.R when key.Modifiers == ConsoleModifiers.Control:
                        ReprintScreen(lines, logs, timezone, dates);
                        break;
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.K:
                        {
                            // If the first line is being displayed, ignore the command
                            if (logs.First().FileOffset == 0)
                            {
                                PrettyConsole.Out.Write("\a");
                                break;
                            }

                            var line = logs.Last();

                            lines = Console.WindowHeight - 1;
                            offset = line.FileOffset;
                            logs = await GetLogsFromFileBackwardsAsync(file, lines, offset, current, before, after, ct).Reverse().ToListAsync(ct);

                            ReprintScreen(lines, logs, timezone, dates);
                            break;
                        }
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.J:
                        {
                            var line = logs.Last();

                            lines = Console.WindowHeight - 1;
                            var calculatedOffset = line.FileOffset + line.LineLength + 1;

                            // If this is the last line in the file, ignore
                            if (calculatedOffset >= file.Length)
                            {
                                PrettyConsole.Out.Write("\a");
                                break;
                            }

                            offset = calculatedOffset;

                            var fetched = await GetLogsFromFileForwardsAsync(file, 1, offset, before, ct).ElementAtAsync(0, ct);

                            // We remove the top line (if needed) and replace it with the one we just fetched at the bottom
                            if (logs.Count >= lines)
                            {
                                logs.RemoveAt(0);
                            }

                            logs.Add(fetched);

                            // We can simply write in this situation
                            PrettyConsole.Out.Write($"{fetched.FileOffset} | ");
                            PrintLogLine(fetched.Log, timezone, dates);

                            break;
                        }
                    case ConsoleKey.PageUp:
                        {
                            var line = logs.First();

                            // If the first line is being displayed, ignore the command
                            if (line.FileOffset == 0)
                            {
                                PrettyConsole.Out.Write("\a");
                                break;
                            }

                            lines = Console.WindowHeight - 1;
                            offset = line.FileOffset;
                            logs = await GetLogsFromFileBackwardsAsync(file, lines, offset, current, before, after, ct).Reverse().ToListAsync(ct);

                            // If didn't fetched enoght lines, we fetch the rest going forwards
                            if (logs.Count != lines)
                            {
                                var last = logs.Last();
                                var logLines = await GetLogsFromFileForwardsAsync(file, lines - logs.Count, last.FileOffset + last.LineLength + 1, before, ct).ToListAsync(ct);

                                logs.AddRange(logLines);
                            }

                            ReprintScreen(lines, logs, timezone, dates);
                            break;
                        }
                    case ConsoleKey.PageDown:
                        {
                            var line = logs.Last();
                            var calculatedOffset = line.FileOffset + line.LineLength + 1;

                            // If this is the last line in the file, ignore
                            if (calculatedOffset >= file.Length)
                            {
                                PrettyConsole.Out.Write("\a");
                                break;
                            }

                            lines = Console.WindowHeight - 1;
                            offset = calculatedOffset;
                            var fetchedLogs = await GetLogsFromFileForwardsAsync(file, lines, offset, before, ct).ToArrayAsync(ct);

                            foreach (var fetchedLog in fetchedLogs)
                            {
                                // We remove the top line (if needed) and replace it with the one we just fetched at the bottom
                                if (logs.Count >= lines)
                                {
                                    logs.RemoveAt(0);
                                }

                                logs.Add(fetchedLog);

                                // We can simply write in this situation
                                PrettyConsole.Out.Write($"{fetchedLog.FileOffset} | ");
                                PrintLogLine(fetchedLog.Log, timezone, dates);
                            }

                            break;
                        }
                }
            }

            await Task.Delay(100, ct);
        }
    }

    private static void ReprintScreen(int expectedLines, List<FileLog> logs, TimeZoneInfo timezone, bool dates)
    {
        Console.Clear();

        // We want to keep the text at the bottom, so we print some text at the top
        var missingLines = expectedLines - logs.Count;
        for (int i = 0; i < missingLines; i++)
        {
            PrettyConsole.Out.WriteLine();
        }

        foreach (var line in logs)
        {
            PrettyConsole.Out.Write($"{line.FileOffset} | ");
            PrintLogLine(line.Log, timezone, dates);
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

    // The following 2 methods are very similar, but the handling is different as one is backwards reading and one is forwards reading
    //  - Backwards reading: given an offset, it starts reading backwards the file and returns the N lines before the offset
    //  - Forwards reading: given an offset, it starts reading forwards and returns the next N lines

    private static async IAsyncEnumerable<FileLog> GetLogsFromFileBackwardsAsync(FileStream file, int lines, long offset, bool currentExecution,
        DateTimeOffset? before, DateTimeOffset? after, [EnumeratorCancellation] CancellationToken ct)
    {
        using var memPoll = MemoryPool<byte>.Shared.Rent(_readBufferSize);
        var sb = new StringBuilder();
        var lineCount = 0;

        // Go to the offset location
        file.Position = Math.Max(offset - _readBufferSize, 0);

        // The buffer may need to be resized to avoid duplicating some data
        var actualBufferSize = (int)Math.Min(_readBufferSize, offset);

        var bytesRead = await file.ReadAsync(memPoll.Memory[0..actualBufferSize], ct);

        // We only want to consume the memory that has data we have just read to avoid some work
        var readMemory = memPoll.Memory[0..bytesRead];

        // The position of the last \n we found. Right after a read this will be the end of the buffer
        var lastNewLine = bytesRead;

        // If the position of the cursor is the same as the size of our (actual) buffer size it means we started reading from 0
        var isEntireStream = file.Position - bytesRead == 0;

        // This is a value used when a line is between chunks, used to store where the line starts, relative to the file.
        long lineOffset = -1;

        while (lineCount < lines)
        {
            var pos = readMemory.Span[0..lastNewLine].LastIndexOf("\n"u8);

            var line = Encoding.UTF8.GetString(readMemory.Span[(pos + 1)..lastNewLine]);
            lastNewLine = pos;

            // If this isn't the entire stream, and we did not found a \n, we need to fetch another chunk
            if (!isEntireStream && pos == -1)
            {
                // Save the start of this line, (if there isn't a line already saved)
                if (lineOffset == -1) lineOffset = file.Position - bytesRead + lastNewLine + 1;

                // We need to store this part of the log line or else we are going to lose it
                sb.Insert(0, line);

                // We don't want to read stuff twice, so we need "resize" our buffer
                actualBufferSize = (int)Math.Min(file.Position - bytesRead, _readBufferSize);

                // Go back to before the read, and get another buffer of space.
                file.Position = Math.Max(file.Position - bytesRead - _readBufferSize, 0);

                bytesRead = await file.ReadAsync(memPoll.Memory[0..actualBufferSize], ct);

                readMemory = memPoll.Memory[0..bytesRead];
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

            // If the log is after the date we want, we want to ignore this one and continue searching.
            if (before is { } b && appLog.Date > b)
            {
                continue;
            }

            // If the log is before the date we want, we can stop now.
            // Since the log file is sorted oldest to newest after we find a line that is before the after filter all the next lines will be before the after as well.
            if (after is { } a && appLog.Date < a)
            {
                break;
            }

            lineCount++;

            // lineOffset is -1 when the line is not between chunks.
            yield return new FileLog(appLog, lineOffset == -1 ? file.Position - bytesRead + lastNewLine + 1 : lineOffset, line.Length);

            lineOffset = -1;

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

    private static async IAsyncEnumerable<FileLog> GetLogsFromFileForwardsAsync(FileStream file, int lines, long offset,
        DateTimeOffset? before, [EnumeratorCancellation] CancellationToken ct)
    {
        using var memPoll = MemoryPool<byte>.Shared.Rent(_readBufferSize);
        var sb = new StringBuilder();
        int lineCount = 0;

        // Go to the offset
        file.Position = offset;

        // The buffer may need to be resized to avoid reading data that isn't needed
        var actualBufferSize = (int)Math.Min(_readBufferSize, file.Length - offset);

        var bytesRead = await file.ReadAsync(memPoll.Memory[0..actualBufferSize], ct);

        // We only want to consume the memory that has data we have just read
        var readMemory = memPoll.Memory[0..bytesRead];

        // The position of the last \n we found. Right after a read this will be 0
        var lastNewLine = 0;

        // In this case, since we read the file forwards the stream ends if we reach the EOF
        var isEntireStream = file.Position == file.Length;

        // This is a value used when a line is between chunks, used to store where the line starts, relative to the file.
        long lineOffset = -1;

        while (lineCount < lines)
        {
            var pos = readMemory.Span[lastNewLine..].IndexOf("\n"u8);

            var line = Encoding.UTF8.GetString(readMemory.Span[lastNewLine..(pos == -1 ? readMemory.Span.Length : lastNewLine + pos)]);

            // The +1 is needed, or else we will finding the same \n over and over again
            lastNewLine += pos + 1;

            // If this isn't the entire stream, and we did not found a \n, we need to fetch another chunk
            if (!isEntireStream && pos == -1)
            {
                // Save the start of this line, (if there isn't a line already saved)
                if (lineOffset == -1) lineOffset = file.Position - bytesRead + lastNewLine - pos - 1;

                // We need to store this part of the log line or else we are going to lose it
                sb.Append(line);

                // We don't want to read stuff twice, so we need "resize" our buffer
                actualBufferSize = (int)Math.Min(file.Length - file.Position, _readBufferSize);

                bytesRead = await file.ReadAsync(memPoll.Memory[0..actualBufferSize], ct);

                readMemory = memPoll.Memory[0..bytesRead];
                lastNewLine = 0;
                isEntireStream = file.Position == file.Length;

                continue;
            }

            // We have buffered a string, use it and clear the StringBuilder to be reused
            if (sb.Length > 0)
            {
                sb.Append(line);
                line = sb.ToString();

                sb.Clear();
            }

            if (!TryParseLogLine(line, out var appLog))
            {
                // If the last line failed to parse, provably due to being empty since the logs have a trailing \n we need to break out
                if (isEntireStream && pos == -1)
                {
                    break;
                }

                continue;
            }

            // If the log is after the date we want, we can stop, as since we are working forwards there won't be dates before this one going on
            if (before is { } b && appLog.Date > b)
            {
                break;
            }

            lineCount++;

            // lineOffset is -1 when the line is not between chunks.
            yield return new FileLog(appLog, lineOffset == -1 ? file.Position - bytesRead + lastNewLine - pos - 1 : lineOffset, line.Length);

            lineOffset = -1;

            // If this was the last line, break out
            if (isEntireStream && pos == -1)
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

    private record struct FileLog(ApplicationLog Log, long FileOffset, int LineLength);

    #endregion

}
