using Spectre.Console;
using System.CommandLine;

namespace Hexus.Commands.Applications;

internal static class StopCommand
{
    private static readonly Argument<string[]> NamesArgument = new("name")
    {
        Description = "The name(s) of the application(s) to stop",
        Arity = ArgumentArity.OneOrMore,
    };
    private static readonly Option<bool> ForceOption = new("--force", "-f")
    {
        Description = "Force the stop of the application",
    };

    public static readonly Command Command = new("stop", "Stop an application")
    {
        NamesArgument,
        ForceOption,
    };

    static StopCommand()
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

        var exitCode = 0;

        foreach (var name in names)
        {
            var stopRequest = await HttpInvocation.DeleteAsync($"Stopping application: {name}", $"/{name}?forceStop={force}", ct);

            if (!stopRequest.IsSuccessStatusCode)
            {
                await HttpInvocation.HandleFailedHttpRequestLogging(stopRequest, ct);
                exitCode = 1;
                continue;
            }

            PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [indianred1]stopped[/]!");
        }

        return exitCode;
    }
}
