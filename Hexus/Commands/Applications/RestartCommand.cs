using Spectre.Console;
using System.CommandLine;

namespace Hexus.Commands.Applications;

internal static class RestartCommand
{
    private static readonly Argument<string[]> NamesArgument = new("name")
    {
        Description = "The name(s) of the application(s) to restart",
        Arity = ArgumentArity.OneOrMore,
    };
    private static readonly Option<bool> ForceOption = new("--force", "-f")
    {
        Description = "Force the restart of the application",
    };

    public static readonly Command Command = new("restart", "Restart an application")
    {
        NamesArgument,
        ForceOption,
    };

    static RestartCommand()
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
            var restartRequest = await HttpInvocation.PatchAsync($"Restarting application: {name}", $"/{name}/restart?forceStop={force}", null, ct);

            if (!restartRequest.IsSuccessStatusCode)
            {
                await HttpInvocation.HandleFailedHttpRequestLogging(restartRequest, ct);
                exitCode = 1;
                continue;
            }

            PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [darkolivegreen1]restarted[/]!");
        }

        return exitCode;
    }
}
