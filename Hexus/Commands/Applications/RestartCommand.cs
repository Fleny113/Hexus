using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands.Applications;

internal static class RestartCommand
{
    private static readonly Argument<string[]> NamesArgument = new("name", "The name(s) of the application(s) to restart")
    {
        Arity = ArgumentArity.OneOrMore
    };
    private static readonly Option<bool> ForceOption = new(["--force", "-f"], "Force the restart of the application");

    public static readonly Command Command = new("restart", "Restart an application")
    {
        NamesArgument,
        ForceOption,
    };

    static RestartCommand()
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
            var restartRequest = await HttpInvocation.PatchAsync($"Restarting application: {name}", $"/{name}/restart?forceStop={force}", null, ct);

            if (!restartRequest.IsSuccessStatusCode)
            {
                await HttpInvocation.HandleFailedHttpRequestLogging(restartRequest, ct);
                context.ExitCode = 1;
                continue;
            }

            PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [darkolivegreen1]restarted[/]!");
        }
    }
}
