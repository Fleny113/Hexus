using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands.Applications;

internal static class StopCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application to stop");
    private static readonly Option<bool> ForceOption = new(["--force", "-f"], "Force the stop of the application");

    public static readonly Command Command = new("stop", "Stop an application")
    {
        NameArgument,
        ForceOption,
    };

    static StopCommand()
    {
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var force = context.ParseResult.GetValueForOption(ForceOption);
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            context.ExitCode = 1;
            return;
        }

        var stopRequest = await HttpInvocation.DeleteAsync("Stopping application", $"/{name}?forceStop={force}", ct);

        if (!stopRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(stopRequest, ct);
            context.ExitCode = 1;
            return;
        }

        PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [indianred1]stopped[/]!");
    }
}
