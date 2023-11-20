using Hexus.Daemon;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands;

internal class DaemonCommand
{
    private static readonly Argument<string[]> DaemonOptions = new("arguments", "The arguments to pass to the daemon")
    {
        Arity = ArgumentArity.ZeroOrMore
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
        StartSubCommand.SetHandler(HandleStartSubCommand);
        StopSubCommand.SetHandler(HandleStopSubCommand);
    }

    private static async Task HandleStartSubCommand(InvocationContext context)
    {
        var args = context.ParseResult.GetValueForArgument(DaemonOptions);

        if (await HttpInvocation.CheckForRunningDaemon())
        {
            Console.Error.WriteLine("There is already daemon a running. Stop it first to run a new instance using the 'daemon stop' command.");
            return;
        }

        HexusDaemon.StartDaemon(args);
    }

    private static async Task HandleStopSubCommand(InvocationContext context)
    {
        if (!await HttpInvocation.CheckForRunningDaemon())
        {
            Console.Error.WriteLine("There is not daemon running. Start it using the 'daemon start' command.");
            return;
        }

        var req = await HttpInvocation.HttpClient.DeleteAsync("/daemon/stop");

        if (!req.IsSuccessStatusCode)
        {
            Console.Error.WriteLine("The was an error stopping the daemon.");
            return;
        }

        Console.WriteLine("Daemon stopped.");
    }
}
