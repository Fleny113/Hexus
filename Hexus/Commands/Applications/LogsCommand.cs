using Hexus.Daemon.Contracts;
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
    private static readonly Option<bool> DontShowDates = new("--no-dates", "Disable the dates of the log lines. Useful if you already have those in your log file");
    private static readonly Option<bool> CurrentExecution = new(["-c", "--current"], "Show logs only from the current or last execution");
    private static readonly Option<DateTime?> ShowLogsAfter = new(["-a", "--after"], "Show logs only after a specified date.");
    private static readonly Option<DateTime?> ShowLogsBefore = new(["-b", "--before"], "Show logs only before a specified date.");
    private static readonly Option<string> Timezone = new(["-t", "--timezone"], "Show the log dates in a specified timezone. The timezone should be compatible with the one provided by your system. Defaults to the local computer timezone.");

    public static readonly Command Command = new("logs", "View the logs of an application")
    {
        NameArgument,
        LinesOption,
        DontStream,
        DontShowDates,
        CurrentExecution,
        ShowLogsAfter,
        ShowLogsBefore,
        Timezone,
    };

    static LogsCommand()
    {
        Timezone.AddValidator(result =>
        {
            var timezone = result.GetValueForOption(Timezone);

            if (timezone is null) return;
            if (TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _)) return;

            result.ErrorMessage = $"The TimeZone was not found on the local computer: {timezone}";
        });

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
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(context.ParseResult.GetValueForOption(Timezone) ?? TimeZoneInfo.Local.Id);
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            context.ExitCode = 1;
            return;
        }

        var showBeforeParam = showBefore is not null ? $"&before={showBefore:O}" : null;
        var showAfterParam = showAfter is not null ? $"&after={showAfter:O}" : null;

        var logsRequest = await HttpInvocation.GetAsync(
            "Getting logs",
            $"/{name}/logs?lines={lines}&noStreaming={noStreaming}&currentExecution={currentExecution}{showBeforeParam}{showAfterParam}",
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

                PrintLogLine(logLine, timezone, !noDates);
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
}
