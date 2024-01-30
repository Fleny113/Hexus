using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands.Applications;

internal static class DeleteCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application to delete");
    private static readonly Option<bool> ForceOption = new(["--force", "-f"], "Force the stop of the application if it needs to be stopped");

    public static readonly Command Command = new("delete", "Stops and delete an application")
    {
        NameArgument,
        ForceOption,
    };

    static DeleteCommand()
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

        var stopRequest = await HttpInvocation.DeleteAsync("Stopping and deleting", $"/{name}/delete?forceStop={force}", ct);

        if (!stopRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(stopRequest, ct);
            context.ExitCode = 1;
            return;
        }

        PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [darkred_1]deleted[/]!");
    }
}
