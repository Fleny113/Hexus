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
using System.Text.RegularExpressions;

namespace Hexus.Commands.Applications;

internal static partial class LogsCommand
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
    # region Filtering options

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

    private static async Task<int> Handler(InvocationContext context)
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

        var streamedLogs = streaming ? await CreateStreamingLogs(name, localizedBefore, ct) : AsyncEnumerable.Empty<ApplicationLog>();

        try
        {
            // While we allow others to write to this file, the expectation is that they will only append. We cannot enforce that sadly.
            using var file = File.Open(logFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // \e[?1049h is the escape sequence to open an alternate screen buffer.
            PrettyConsole.Out.Write("\e[?1049h");
            Console.SetCursorPosition(0, 0);

            var displayOptions = new DisplayOptions(currentExecution, timeZoneInfo, localizedBefore, localizedAfter, !noDates);

            await HandlerCore(file, displayOptions, streamedLogs, ct);
            return 0;
        }
        catch (TaskCanceledException)
        {
            // We really don't care for a TaskCancelledException
            return 0;
        }
        finally
        {
            // \e[?1049l is the escape sequence to close the alternate screen buffer.
            PrettyConsole.Out.Write("\e[?1049l");
        }
    }

    private static async Task HandlerCore(FileStream file, DisplayOptions displayOptions, IAsyncEnumerable<ApplicationLog?> streamedLogs, CancellationToken ct)
    {
        var logs = await GetLogsFromFileBackwardsAsync(
            file,
            displayOptions.PageHeight,
            offset: file.Length,
            displayOptions.ShowCurrentExecution,
            displayOptions.BeforeDate,
            displayOptions.AfterDate,
            ct
        ).Reverse().ToListAsync(ct);

        ReprintScreen(logs, displayOptions);

        var streamingEnumerator = streamedLogs.GetAsyncEnumerator(ct);
        var streamingTask = streamingEnumerator.MoveNextAsync().AsTask();

        while (!ct.IsCancellationRequested)
        {
            var resized = displayOptions.WasConsoleResized(out var heightChanged);

            if (Console.KeyAvailable)
            {
                logs = await HandleKeyInput(file, displayOptions, logs, ct);
            }

            if (resized)
            {
                logs = await HandleConsoleResize(file, displayOptions, logs, heightChanged, ct);
            }

            await Task.WhenAny(
                Task.Delay(100, ct),
                streamingTask
            );

            if (streamingTask is { IsCompletedSuccessfully: true, Result: bool taskResult })
            {
                if (taskResult)
                {
                    streamingTask = streamingEnumerator.MoveNextAsync().AsTask();
                }

                await HandleStreamingTask(file, displayOptions, logs, streamingEnumerator, ct);
            }
        }
    }

    private static void ReprintScreen(List<FileLog> logs, DisplayOptions displayOptions)
    {
        var sb = new StringBuilder();

        // We want to keep the text at the bottom, so we print some text at the top
        var missingLines = displayOptions.PageHeight - logs.Count;
        for (int i = 0; i < missingLines; i++)
        {
            sb.AppendLine();
        }

        foreach (var line in logs)
        {
            sb.AppendLine(GetLogLine(line, displayOptions.TimeZone, displayOptions.ShowDates, displayOptions.PageOffset, displayOptions.PageWidth));
        }

        PrettyConsole.OutLimitlessWidth.Markup(sb.ToString());
    }


    private static readonly string AnsiReset = "\e[0m\e]8;;\e\\".EscapeMarkup();

    private static string GetLogLine(FileLog line, TimeZoneInfo timeZoneInfo, bool showDates, int offset, int width)
    {
        var color = GetLogTypeColor(line.Log.LogType);

        var timezone = TimeZoneInfo.ConvertTime(line.Log.Date, timeZoneInfo);
        var date = showDates ? $"{timezone:yyyy-MM-dd HH:mm:ss} [{color}]| " : $"[{color}]";

        var header = $"{date}{line.Log.LogType} |[/] ";

        var headerLen = Markup.Remove(header).Length;
        var startLen = width - headerLen;

        var logText = PaginateString(line.Log.Text, offset, offset == 0 ? startLen : width, out var hadMore);

        var seeMoreArrow = hadMore ? "[black on white]>[/]" : "";

        // When the offset is not 0 do not show the header
        if (offset > 0)
        {
            return $"{logText.EscapeMarkup()}{AnsiReset}{seeMoreArrow}";
        }

        return $"{header}{logText.EscapeMarkup()}{AnsiReset}{seeMoreArrow}";
    }

    private static Color GetLogTypeColor(LogType logType) => logType switch
    {
        LogType.STDOUT => Color.SpringGreen3,
        LogType.STDERR => Color.Red3_1,
        LogType.SYSTEM => Color.MediumPurple2,
        _ => throw new ArgumentOutOfRangeException(nameof(logType), "The requested log type is not mapped to a color"),
    };

    private static void ConsoleBell() => PrettyConsole.Out.Write("\a");

    private static void PrependLine(List<FileLog> logs, DisplayOptions displayOptions, FileLog fetched)
    {
        if (logs.Count >= displayOptions.PageHeight)
        {
            logs.RemoveAt(0);
        }

        logs.Add(fetched);
    }

    #region String length pagination

    // The string could be very long and we want to fit it in one screen only due to the fact that scrollback is disabled (A virtual screen buffer disables scrollback)
    // However since we have some "Header" to the line line (output type and [optionally] the date) we need the first split to be shorter based on the header length

    private static string PaginateString(ReadOnlySpan<char> text, int offset, int stringLen, out bool hadMore)
    {
        hadMore = false;

        var ansiSequences = AnsiRegex().EnumerateMatches(text);
        var startIndex = stringLen / 2 * offset;

        var sb = new StringBuilder();

        // To preserve all formatting we need to prepend all ANSI and OSC8 sequences before the actual text
        foreach (var ansi in ansiSequences)
        {
            // If we found an ansi sequence after where our start index is we can break out
            if (ansi.Index >= startIndex) break;

            // If we mapped in between an ansi sequence we move the start index
            if (startIndex >= ansi.Index && startIndex <= ansi.Index + ansi.Length)
            {
                startIndex = ansi.Index + ansi.Length;
            }

            // Take the ANSI sequence
            sb.Append(text.Slice(ansi.Index, ansi.Length));
        }


        if (text.Length < startIndex) return "";

        // If don't have stringLen chars, so we can just take all from the remainder
        if (stringLen >= text.Length - startIndex)
        {
            sb.Append(text[startIndex..]);
        }
        // If we have more then stringLen chars we need to be carful as we don't want to consider ANSI chars to part of this
        else
        {
            // We re-define the enumerator because now we have a startIndex
            ansiSequences = AnsiRegex().EnumerateMatches(text, startIndex);
            var found = ansiSequences.MoveNext();
            var takenChars = 0;

            for (var i = startIndex; i < text.Length; i++)
            {
                // If we found the ansi sequence take it
                if (found && ansiSequences.Current.Index == i)
                {
                    sb.Append(text.Slice(ansiSequences.Current.Index, ansiSequences.Current.Length));
                    // Skip the sequence, we need the -1 to take account of the i++
                    i = ansiSequences.Current.Index + ansiSequences.Current.Length - 1;
                    // Find the next ansi sequence
                    found = ansiSequences.MoveNext();

                    continue;
                }

                // If we taken the chars we needed we can exit
                if (takenChars++ >= stringLen)
                {
                    hadMore = true;
                    break;
                }

                sb.Append(text[i]);
            }
        }

        return sb.ToString();
    }

    // Regex taken from the npm package "ansi-regex" (https://github.com/chalk/ansi-regex/blob/9cba40dc3df00ee7316c01db4955d31ef7527012/index.js)
    [GeneratedRegex(@"[\u001B\u009B][[\]()#;?]*(?:(?:(?:(?:;[-a-zA-Z\d\/#&.:=?%@~_]+)*|[a-zA-Z\d]+(?:;[-a-zA-Z\d\/#&.:=?%@~_]*)*)?(?:\u0007|\u001B\u005C|\u009C))|(?:(?:\d{1,4}(?:;\d{0,4})*)?[\dA-PR-TZcf-nq-uy=><~]))")]
    private static partial Regex AnsiRegex();

    #endregion

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

                // We need to store this part of the line line or else we are going to lose it
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

                // We need to store this part of the line line or else we are going to lose it
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

            // If the line is after the date we want, we can stop, as since we are working forwards there won't be dates before this one going on
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

    #region Streaming logs

    private static async Task<IAsyncEnumerable<ApplicationLog?>> CreateStreamingLogs(string name, DateTime? before, CancellationToken ct)
    {
        var showBeforeParam = before is not null ? $"before={before:O}" : null;

        var logsRequest = await HttpInvocation.HttpClient.GetAsync(
            $"/{name}/logs?{showBeforeParam}",
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );

        if (!logsRequest.IsSuccessStatusCode)
        {
            PrettyConsole.Out.MarkupLine("""
                    [yellow1]Warning[/]: Streaming was enabled, but the [indianred1]daemon returned an error[/].

                    [italic]Press any key to continue.[/]
                    [italic gray]See below for more details:[/]
                    """);

            await HttpInvocation.HandleFailedHttpRequestLogging(logsRequest, ct);
            Console.ReadKey(true);

            return AsyncEnumerable.Empty<ApplicationLog>();
        }

        return logsRequest.Content.ReadFromJsonAsAsyncEnumerable<ApplicationLog>(HttpInvocation.JsonSerializerOptions, ct);
    }

    private static async Task HandleStreamingTask(FileStream file, DisplayOptions displayOptions, List<FileLog> logs,
        IAsyncEnumerator<ApplicationLog?> streamingEnumerator, CancellationToken ct)
    {
        if (displayOptions.IgnoreStream || streamingEnumerator.Current is null) return;

        var line = logs.Last();
        var calculatedOffset = line.FileOffset + line.LineLength + 1;

        // If this is the last line in the file, ignore
        if (calculatedOffset >= file.Length)
        {
            ConsoleBell();
            return;
        }

        // TODO: do we have a way to avoid fetching from the file and use what the daemon gives us?
        // The daemon gives us a Applicationlog, use need a FileLog for the tl;dr of the issue

        var logLine = await GetLogsFromFileForwardsAsync(file, 1, calculatedOffset, displayOptions.BeforeDate, ct).ElementAtAsync(0, ct);

        // We remove the top line (if needed) and replace it with the one we just fetched at the bottom
        if (logs.Count >= displayOptions.PageHeight)
        {
            logs.RemoveAt(0);
        }

        logs.Add(logLine);

        ReprintScreen(logs, displayOptions);
    }

    #endregion

    #region User input

    private static async Task<List<FileLog>> HandleKeyInput(FileStream file, DisplayOptions displayOptions, List<FileLog> logs, CancellationToken ct)
    {
        var key = Console.ReadKey(true);

        switch (key.Key)
        {
            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                throw new TaskCanceledException();

            case ConsoleKey.R when key.Modifiers == ConsoleModifiers.Control:
                ReprintScreen(logs, displayOptions);
                break;

            // Up & Down

            case ConsoleKey.UpArrow:
            case ConsoleKey.K:
                logs = await ScrollUp(file, displayOptions, logs, ct);
                break;

            case ConsoleKey.DownArrow:
            case ConsoleKey.J:
                await ScrollDown(file, displayOptions, logs, ct);
                break;

            // Page keys

            case ConsoleKey.PageUp:
                logs = await PageUp(file, displayOptions, logs, ct);
                break;

            case ConsoleKey.PageDown:
                await PageDown(file, displayOptions, logs, ct);
                break;

            // "g" & "G"

            case ConsoleKey.G when key.Modifiers == ConsoleModifiers.Shift:
                logs = await GoToBotton(file, displayOptions, ct);
                break;

            case ConsoleKey.G:
                logs = await GoToTop(file, displayOptions, ct);
                break;

            // Left and right

            // CTRL-RightArrow is not implemented as i'm not sure how to implement it

            case ConsoleKey.LeftArrow when key.Modifiers == ConsoleModifiers.Control:
                GoToLeft(displayOptions, logs);
                break;

            case ConsoleKey.RightArrow:
                RightArrow(displayOptions, logs);
                break;

            case ConsoleKey.LeftArrow:
                LeftArrow(displayOptions, logs);
                break;
        }

        return logs;
    }

    private static async Task<List<FileLog>> ScrollUp(FileStream file, DisplayOptions displayOptions, List<FileLog> logs, CancellationToken ct)
    {
        // If the first line is being displayed, ignore the command
        if (logs.First().FileOffset == 0)
        {
            ConsoleBell();
            return logs;
        }

        var line = logs.Last();
        logs = await GetLogsFromFileBackwardsAsync(
            file,
            displayOptions.PageHeight,
            line.FileOffset,
            displayOptions.ShowCurrentExecution,
            displayOptions.BeforeDate,
            displayOptions.AfterDate,
            ct
        ).Reverse().ToListAsync(ct);

        displayOptions.IgnoreStream = true;
        ReprintScreen(logs, displayOptions);

        return logs;
    }

    private static async Task ScrollDown(FileStream file, DisplayOptions displayOptions, List<FileLog> logs, CancellationToken ct)
    {
        var line = logs.Last();
        var calculatedOffset = line.FileOffset + line.LineLength + 1;

        // If this is the last line in the file, ignore
        if (calculatedOffset >= file.Length)
        {
            ConsoleBell();
            return;
        }

        var fetched = await GetLogsFromFileForwardsAsync(file, lines: 1, calculatedOffset, displayOptions.BeforeDate, ct).ElementAtOrDefaultAsync(0, ct);

        if (fetched == default) return;

        PrependLine(logs, displayOptions, fetched);

        line = logs.Last();
        calculatedOffset = line.FileOffset + line.LineLength + 1;

        // If the new last line is the last line in the file, resume streaming
        displayOptions.IgnoreStream = calculatedOffset != file.Length;
        ReprintScreen(logs, displayOptions);
    }

    private static async Task<List<FileLog>> PageUp(FileStream file, DisplayOptions displayOptions, List<FileLog> logs, CancellationToken ct)
    {
        var line = logs.First();

        // If the first line is being displayed, ignore the command
        if (line.FileOffset == 0)
        {
            ConsoleBell();
            return logs;
        }

        logs = await GetLogsFromFileBackwardsAsync(
            file,
            displayOptions.PageHeight,
            offset: line.FileOffset,
            displayOptions.ShowCurrentExecution,
            displayOptions.BeforeDate,
            displayOptions.AfterDate,
            ct
        ).Reverse().ToListAsync(ct);

        // If didn't fetched enoght lines, we fetch the rest going forwards
        if (logs.Count != displayOptions.PageHeight)
        {
            var last = logs.Last();
            var logLines = await GetLogsFromFileForwardsAsync(
                file,
                lines: displayOptions.PageHeight - logs.Count,
                offset: last.FileOffset + last.LineLength + 1,
                displayOptions.BeforeDate,
                ct
            ).ToListAsync(ct);

            logs.AddRange(logLines);
        }

        displayOptions.IgnoreStream = true;
        ReprintScreen(logs, displayOptions);

        return logs;
    }

    private static async Task PageDown(FileStream file, DisplayOptions displayOptions, List<FileLog> logs, CancellationToken ct)
    {
        var line = logs.Last();
        var calculatedOffset = line.FileOffset + line.LineLength + 1;

        // If this is the last line in the file, ignore
        if (calculatedOffset >= file.Length)
        {
            ConsoleBell();
            return;
        }

        var fetchedLogs = GetLogsFromFileForwardsAsync(file, displayOptions.PageHeight, calculatedOffset, displayOptions.BeforeDate, ct);

        await foreach (var fetchedLog in fetchedLogs)
        {
            PrependLine(logs, displayOptions, fetchedLog);
        }

        line = logs.Last();
        calculatedOffset = line.FileOffset + line.LineLength + 1;

        // If the new last line is the last line in the file, resume streaming
        displayOptions.IgnoreStream = calculatedOffset != file.Length;
        ReprintScreen(logs, displayOptions);
    }

    private static async Task<List<FileLog>> GoToTop(FileStream file, DisplayOptions displayOptions, CancellationToken ct)
    {
        var logs = await GetLogsFromFileForwardsAsync(file, displayOptions.PageHeight, offset: 0, displayOptions.BeforeDate, ct).ToListAsync(ct);

        displayOptions.IgnoreStream = true;
        ReprintScreen(logs, displayOptions);
        return logs;
    }

    private static async Task<List<FileLog>> GoToBotton(FileStream file, DisplayOptions displayOptions, CancellationToken ct)
    {
        var logs = await GetLogsFromFileBackwardsAsync(
            file,
            displayOptions.PageHeight,
            offset: file.Length,
            displayOptions.ShowCurrentExecution,
            displayOptions.BeforeDate,
            displayOptions.AfterDate,
            ct
        ).Reverse().ToListAsync(ct);

        displayOptions.IgnoreStream = false;
        ReprintScreen(logs, displayOptions);
        return logs;
    }

    private static void GoToLeft(DisplayOptions displayOptions, List<FileLog> logs)
    {
        displayOptions.PageOffset = 0;

        ReprintScreen(logs, displayOptions);
    }

    private static void RightArrow(DisplayOptions displayOptions, List<FileLog> logs)
    {
        displayOptions.PageOffset++;

        ReprintScreen(logs, displayOptions);
    }

    private static void LeftArrow(DisplayOptions displayOptions, List<FileLog> logs)
    {
        // We don't want to go into a negative offset
        if (displayOptions.PageOffset == 0)
        {
            ConsoleBell();
            return;
        }

        displayOptions.PageOffset--;

        ReprintScreen(logs, displayOptions);
    }

    private static async Task<List<FileLog>> HandleConsoleResize(FileStream file, DisplayOptions displayOptions, List<FileLog> logs, bool heightChanged, CancellationToken ct)
    {
        if (heightChanged)
        {
            var line = logs.Last();
            var calculatedOffset = line.FileOffset + line.LineLength + 1;

            // If this is the last line in the file, ignore
            if (calculatedOffset >= file.Length)
            {
                ConsoleBell();
                return logs;
            }

            logs = await GetLogsFromFileBackwardsAsync(
                file, displayOptions.PageHeight, calculatedOffset, displayOptions.ShowCurrentExecution,
                displayOptions.BeforeDate, displayOptions.AfterDate, ct
            ).Reverse().ToListAsync(ct);
        }

        ReprintScreen(logs, displayOptions);
        return logs;
    }

    #endregion

    private record DisplayOptions
    {
        public bool ShowCurrentExecution { get; init; }
        public TimeZoneInfo TimeZone { get; init; }
        public DateTimeOffset? BeforeDate { get; init; }
        public DateTimeOffset? AfterDate { get; init; }
        public bool ShowDates { get; init; }
        public int PageWidth { get; private set; }
        public int PageHeight { get; private set; }
        public int PageOffset { get; set; }
        public bool IgnoreStream { get; set; }

        public DisplayOptions(bool current, TimeZoneInfo timezone, DateTimeOffset? before, DateTimeOffset? after, bool dates)
        {
            ShowCurrentExecution = current;
            TimeZone = timezone;
            BeforeDate = before;
            AfterDate = after;
            ShowDates = dates;

            PageWidth = Console.BufferWidth - 1;
            PageHeight = Console.BufferHeight - 1;
            PageOffset = 0;
            IgnoreStream = false;
        }

        public bool WasConsoleResized(out bool heightChanged)
        {
            var width = Console.BufferWidth - 1;
            var widthChanged = width != PageWidth;

            var height = Console.BufferHeight - 1;
            heightChanged = height != PageHeight;

            PageWidth = width;
            PageHeight = height;

            return widthChanged || heightChanged;
        }
    }
}
