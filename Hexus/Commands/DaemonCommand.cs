using Hexus.Daemon;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands;

internal class DaemonCommand
{
    private static readonly Argument<string[]> DaemonOptions = new("arguments", "The arguments to pass to the daemon")
    {
        Arity = ArgumentArity.ZeroOrMore,
    };

    private static readonly Command StartSubCommand = new("start", "Start the Hexus daemon") { DaemonOptions };
    private static readonly Command StopSubCommand = new("stop", "Stop the currently running Hexus daemon");

    public static readonly Command Command = new("daemon", "Manage the Hexus daemon")
    {
        StartSubCommand,
        StopSubCommand,
    };

    static DaemonCommand()
    {
        StartSubCommand.SetHandler(StartSubCommandHandler);
        StopSubCommand.SetHandler(StopSubCommandHandler);
    }

    private static async Task StartSubCommandHandler(InvocationContext context)
    {
        var args = context.ParseResult.GetValueForArgument(DaemonOptions);
        var ct = context.GetCancellationToken();

        if (await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonAlreadyRunningError);
            return;
        }

        HexusDaemon.StartDaemon(args);
    }

    private static async Task StopSubCommandHandler(InvocationContext context)
    {
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return;
        }

        var req = await HttpInvocation.HttpClient.DeleteAsync("/daemon/stop", ct);

        if (!req.IsSuccessStatusCode)
        {
            PrettyConsole.Error.MarkupLine("There [indianred1]was an error[/] stopping the [indianred1]daemon[/].");
            return;
        }

        PrettyConsole.Out.MarkupLine("[indianred1]Daemon[/] stopped.");
    }
}
