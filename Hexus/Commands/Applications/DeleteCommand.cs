using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands.Applications;

internal static class DeleteCommand
{
    private static readonly Argument<string[]> NamesArgument = new("name", "The name(s) of the application(s) to delete")
    {
        Arity = ArgumentArity.OneOrMore
    };
    private static readonly Option<bool> ForceOption = new(["--force", "-f"], "Force the stop of the application if it needs to be stopped");

    public static readonly Command Command = new("delete", "Stops and delete an application")
    {
        NamesArgument,
        ForceOption,
    };

    static DeleteCommand()
    {
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var names = context.ParseResult.GetValueForArgument(NamesArgument);
        var force = context.ParseResult.GetValueForOption(ForceOption);
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            context.ExitCode = 1;
            return;
        }

        foreach (var name in names)
        {
            var stopRequest = await HttpInvocation.DeleteAsync($"Stopping and deleting: {name}", $"/{name}/delete?forceStop={force}", ct);

            if (!stopRequest.IsSuccessStatusCode)
            {
                await HttpInvocation.HandleFailedHttpRequestLogging(stopRequest, ct);
                context.ExitCode = 1;
                return;
            }

            PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [darkred_1]deleted[/]!");
        }
    }
}
