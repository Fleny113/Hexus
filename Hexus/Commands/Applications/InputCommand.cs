using Hexus.Daemon.Contracts.Requests;
using Spectre.Console;
using System.CommandLine;

namespace Hexus.Commands.Applications;

internal static class InputCommand
{
    private static readonly Argument<string> NameArgument = new("name")
    {
        Description = "The name of the application",
    };

    private static readonly Argument<string[]> InputArgument = new("input")
    {
        Description = "The text to send in the STDIN of the application",
        Arity = ArgumentArity.OneOrMore,
    };

    private static readonly Option<bool> DontAddNewLineOption = new("--remove-new-line", "--no-new-line", "-n")
    {
        Description = "Don't add a new line at the end of the text",
    };

    public static readonly Command Command = new("input", "Get the information for an application")
    {
        NameArgument,
        InputArgument,
        DontAddNewLineOption,
    };

    static InputCommand()
    {
        Command.SetAction(Handler);
    }

    private static async Task<int> Handler(ParseResult parseResult, CancellationToken ct)
    {
        var name = parseResult.GetRequiredValue(NameArgument);
        var text = string.Join(' ', parseResult.GetRequiredValue(InputArgument));
        var newLine = !parseResult.GetValue(DontAddNewLineOption);

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return 1;
        }

        var stdinRequest = await HttpInvocation.PostAsJsonAsync("Sending text to STDIN",
            $"/{name}/stdin",
            new SendInputRequest(text, newLine),
            HttpInvocation.JsonSerializerContext,
            ct);

        if (!stdinRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(stdinRequest, ct);
            return 1;
        }

        PrettyConsole.Out.MarkupLineInterpolated($"Sent text to the application \"{name}\".");

        return 0;
    }
}
