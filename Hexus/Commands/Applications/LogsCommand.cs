using Hexus.Daemon.Configuration;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;

namespace Hexus.Commands.Applications;

internal static class LogsCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application");
    private static readonly Option<int?> LinesOption = new(["-l", "--lines"], "The number of lines to show from the log file");
    private static readonly Option<bool> DontStream = new("--no-streaming", "Disable the streaming of new logs. It Will only fetch from the log file");

    private static readonly Option<bool> DontShowDates = new("--no-dates",
        "Disable the dates of the log lines. Useful if you already have those in your log file");

    private static readonly Option<DateTime?> ShowLogsAfter = new(["-a", "--after"], "Show logs only after a specified date.");
    private static readonly Option<DateTime?> ShowLogsBefore = new(["-b", "--before"], "Show logs only before a specified date.");

    public static readonly Command Command = new("logs", "View the logs of an application")
    {
        NameArgument,
        LinesOption,
        DontStream,
        DontShowDates,
        ShowLogsAfter,
        ShowLogsBefore,
    };

    static LogsCommand()
    {
        Command.AddAlias("log");
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var lines = context.ParseResult.GetValueForOption(LinesOption) ?? 10;
        var noStreaming = context.ParseResult.GetValueForOption(DontStream);
        var noDates = context.ParseResult.GetValueForOption(DontShowDates);
        var showAfter = context.ParseResult.GetValueForOption(ShowLogsAfter);
        var showBefore = context.ParseResult.GetValueForOption(ShowLogsBefore);
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            context.ExitCode = 1;
            return;
        }

        var showBeforeParam = showBefore is not null ? $"&before={showBefore}" : null;
        var showAfterParam = showAfter is not null ? $"&after={showAfter}" : null;

        var logsRequest = await HttpInvocation.GetAsync(
            "Getting logs",
            $"/{name}/logs?lines={lines}&noStreaming={noStreaming}{showBeforeParam}{showAfterParam}",
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );

        if (!logsRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(logsRequest, ct);
            context.ExitCode = 1;
            return;
        }

        try
        {
            var logs = logsRequest.Content.ReadFromJsonAsAsyncEnumerable<ApplicationLog>(HttpInvocation.JsonSerializerOptions, ct);

            await foreach (var logLine in logs)
            {
                if (logLine is null) continue;

                PrintLogLine(logLine, !noDates);
            }
        }
        catch (TaskCanceledException)
        {
            // Discard the exception
        }
    }

    private static void PrintLogLine(ApplicationLog log, bool showDates)
    {
        var baseLogLine = $"{log.LogType.Name} |[/] {log.Text.EscapeMarkup()}";
        var color = GetLogTypeColor(log.LogType.Name);

        if (showDates)
        {
            PrettyConsole.OutLimitlessWidth.MarkupLine($"{log.Date.ToString(ApplicationLog.DateTimeFormat)} [{color}]| {baseLogLine}");
            return;
        }

        PrettyConsole.OutLimitlessWidth.MarkupLine($"[{color}]{baseLogLine}");
    }

    private static Color GetLogTypeColor(ReadOnlySpan<char> logType) => logType switch
    {
        "STDOUT" => Color.SpringGreen3,
        "STDERR" => Color.Red3_1,
        "SYSTEM" => Color.MediumPurple2,
        _ => throw new ArgumentOutOfRangeException(nameof(logType), "The requested log type is not mapped to a color"),
    };
}
