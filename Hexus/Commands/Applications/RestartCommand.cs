using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands.Applications;

internal static class RestartCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application to restart");
    private static readonly Option<bool> ForceOption = new(["--force", "-f"], "Force the restart of the application");
    
    public static readonly Command Command = new("restart", "Restart an application")
    {
        NameArgument,
        ForceOption,
    };

    static RestartCommand()
    {        
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var force = context.ParseResult.GetValueForOption(ForceOption);
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return;
        }

        var restartRequest = await HttpInvocation.HttpClient.PatchAsync($"/{name}/restart?forceStop={force}", null, ct);

        if (!restartRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(restartRequest, ct);
            return;
        }

        PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [darkolivegreen1]restarted[/]!");
    }
}
