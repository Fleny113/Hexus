using Spectre.Console;
using System.CommandLine;

namespace Hexus.Commands;

internal static class DaemonCommand
{
    private static readonly Option<bool> ShowSocketPath = new("--socket")
    {
        Description = "Show the socket file being used. Useful for debugging",
    };

    private static readonly Command StopSubCommand = new("stop", "Stop the currently running Hexus daemon");
    private static readonly Command StatusSubCommand = new("status", "Gets the current status of the Hexus daemon") { ShowSocketPath };

    public static readonly Command Command = new("daemon", "Manage the Hexus daemon")
    {
        StopSubCommand,
        StatusSubCommand,
    };

    static DaemonCommand()
    {
        StopSubCommand.SetAction(StopSubCommandHandler);
        StatusSubCommand.SetAction(StatusSubCommandHandler);
    }

    private static async Task StopSubCommandHandler(ParseResult parseResult, CancellationToken ct)
    {
        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return;
        }

        var req = await HttpInvocation.DeleteAsync("Stopping the running daemon", "/daemon/stop", ct);

        if (!req.IsSuccessStatusCode)
        {
            PrettyConsole.Error.MarkupLine("There [indianred1]was an error[/] stopping the [indianred1]daemon[/].");
            return;
        }

        PrettyConsole.Out.MarkupLine("[indianred1]Daemon[/] stopped.");
    }

    private static async Task StatusSubCommandHandler(ParseResult parseResult, CancellationToken ct)
    {
        var showSocket = parseResult.GetValue(ShowSocketPath);

        var isRunning = await HttpInvocation.CheckForRunningDaemon(ct);

        if (showSocket)
        {
            PrettyConsole.Out.MarkupLineInterpolated($"[gray]Socket being used: [link italic]{Configuration.HexusConfiguration.UnixSocket}[/][/]");
        }

        PrettyConsole.Out.MarkupLine($"The daemon is {(isRunning ? "[springgreen1]running[/]" : "[indianred1]not running[/]")}.");
    }
}
