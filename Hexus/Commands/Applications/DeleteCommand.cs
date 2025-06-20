using Spectre.Console;
using System.CommandLine;

namespace Hexus.Commands.Applications;

internal static class DeleteCommand
{
    private static readonly Argument<string[]> NamesArgument = new("name")
    {
        Description = "The name(s) of the application(s) to delete. You can specify multiple names separated by spaces.",
        Arity = ArgumentArity.OneOrMore,
    };
    private static readonly Option<bool> ForceOption = new("--force", "-f")
    {
        Description = "Force the stop of the application if it needs to be stopped",
    };

    public static readonly Command Command = new("delete", "Stops and delete an application")
    {
        NamesArgument,
        ForceOption,
    };

    static DeleteCommand()
    {
        Command.SetAction(Handler);
    }

    private static async Task<int> Handler(ParseResult parseResult, CancellationToken ct)
    {
        var names = parseResult.GetRequiredValue(NamesArgument);
        var force = parseResult.GetValue(ForceOption);

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return 1;
        }

        foreach (var name in names)
        {
            var stopRequest = await HttpInvocation.DeleteAsync($"Stopping and deleting: {name}", $"/{name}/delete?forceStop={force}", ct);

            if (!stopRequest.IsSuccessStatusCode)
            {
                await HttpInvocation.HandleFailedHttpRequestLogging(stopRequest, ct);
                return 1;
            }

            PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [darkred_1]deleted[/]!");
        }

        return 0;
    }
}
