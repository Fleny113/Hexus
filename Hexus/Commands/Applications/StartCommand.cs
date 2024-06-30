using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands.Applications;

internal static class StartCommand
{
    private static readonly Argument<string[]> NamesArgument = new("name", "The name(s) of the application(s) to start")
    {
        Arity = ArgumentArity.OneOrMore
    };

    public static readonly Command Command = new("start", "Start an exiting application")
    {
        NamesArgument,
    };

    static StartCommand()
    {
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var names = context.ParseResult.GetValueForArgument(NamesArgument);
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            context.ExitCode = 1;
            return;
        }

        foreach (var name in names)
        {
            var startRequest = await HttpInvocation.PostAsync($"Starting application: {name}", $"/{name}", null, ct);

            if (!startRequest.IsSuccessStatusCode)
            {
                await HttpInvocation.HandleFailedHttpRequestLogging(startRequest, ct);
                context.ExitCode = 1;
                continue;
            }

            PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [darkseagreen1_1]started[/]!");
        }
    }
}
