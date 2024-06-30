using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands.Applications;

internal static class StopCommand
{
    private static readonly Argument<string[]> NamesArgument = new("name", "The name(s) of the application(s) to stop")
    {
        Arity = ArgumentArity.OneOrMore
    };
    private static readonly Option<bool> ForceOption = new(["--force", "-f"], "Force the stop of the application");

    public static readonly Command Command = new("stop", "Stop an application")
    {
        NamesArgument,
        ForceOption,
    };

    static StopCommand()
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
            var stopRequest = await HttpInvocation.DeleteAsync($"Stopping application: {name}", $"/{name}?forceStop={force}", ct);

            if (!stopRequest.IsSuccessStatusCode)
            {
                await HttpInvocation.HandleFailedHttpRequestLogging(stopRequest, ct);
                context.ExitCode = 1;
                continue;
            }

            PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [indianred1]stopped[/]!");
        }
    }
}
