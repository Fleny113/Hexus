using Spectre.Console;
using System.CommandLine;

namespace Hexus.Commands.Applications;

internal static class StartCommand
{
    private static readonly Argument<string[]> NamesArgument = new("name")
    {
        Description = "The name(s) of the application(s) to start",
        Arity = ArgumentArity.OneOrMore,
    };

    public static readonly Command Command = new("start", "Start an exiting application")
    {
        NamesArgument,
    };

    static StartCommand()
    {
        Command.SetAction(Handler);
    }

    private static async Task<int> Handler(ParseResult parseResult, CancellationToken ct)
    {
        var names = parseResult.GetRequiredValue(NamesArgument);

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return 1;
        }

        var exitCode = 0;

        foreach (var name in names)
        {
            var startRequest = await HttpInvocation.PostAsync($"Starting application: {name}", $"/{name}", null, ct);

            if (!startRequest.IsSuccessStatusCode)
            {
                await HttpInvocation.HandleFailedHttpRequestLogging(startRequest, ct);
                exitCode = 1;
                continue;
            }

            PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [darkseagreen1_1]started[/]!");
        }

        return exitCode;
    }
}
