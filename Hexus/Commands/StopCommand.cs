using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands;

internal static class StopCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application to stop");
    private static readonly Option<bool> ForceOption = new("--force", "Force the stop of the application");
    
    public static readonly Command Command = new("stop", "Stop an application")
    {
        NameArgument,
        ForceOption,
    };

    static StopCommand()
    {
        ForceOption.AddAlias("-f");
        
        Command.SetHandler(HandleAsync);
    }
    
    private static Task HandleAsync(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var force = context.ParseResult.GetValueForOption(ForceOption);
        
        var toForceStop = force ? "force" : "";
    
        Console.WriteLine($"Requested to {toForceStop}stop an application named: \"{name}\"");

        return Task.CompletedTask;
    }
}
