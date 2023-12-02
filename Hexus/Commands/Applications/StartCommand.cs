using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands.Applications;

internal static class StartCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application to start");

    public static readonly Command Command = new("start", "Start an exiting application")
    {
        NameArgument,
    };

    static StartCommand()
    {
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return;
        }

        var startRequest = await HttpInvocation.HttpClient.PostAsync($"/{name}", null, ct);

        if (!startRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(startRequest, ct);
            return;
        }

        PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [darkseagreen1_1]started[/]!");
    }
}
