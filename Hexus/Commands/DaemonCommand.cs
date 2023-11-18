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
    
    public static readonly Command Command = new("daemon", "Manage the Hexus daemon")
    {
        StartSubCommand
    };

    static DaemonCommand()
    {
        StartSubCommand.SetHandler(HandleStartSubCommand);
    }

    private static void HandleStartSubCommand(InvocationContext context)
    {
        var args = context.ParseResult.GetValueForArgument(DaemonOptions);
        
        HexusDaemon.StartDaemon(args);
    }
}
