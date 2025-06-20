using Hexus.Daemon.Contracts.Requests;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands.Applications;

internal static class InputCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application");

    private static readonly Argument<string[]> InputArgument = new("input", "The text to send in the STDIN of the application")
    {
        Arity = ArgumentArity.OneOrMore,
    };

    private static readonly Option<bool> DontAddNewLineOption = new(["-n", "--remove-new-line"], "Don't add a new line at the end of the text");

    public static readonly Command Command = new("input", "Get the information for an application")
    {
        NameArgument,
        InputArgument,
        DontAddNewLineOption,
    };

    static InputCommand()
    {
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var text = string.Join(' ', context.ParseResult.GetValueForArgument(InputArgument));
        var newLine = !context.ParseResult.GetValueForOption(DontAddNewLineOption);
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            context.ExitCode = 1;
            return;
        }

        var stdinRequest = await HttpInvocation.PostAsJsonAsync("Sending text to STDIN",
            $"/{name}/stdin",
            new SendInputRequest(text, newLine),
            HttpInvocation.JsonSerializerContext,
            ct);

        if (!stdinRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(stdinRequest, ct);
            context.ExitCode = 1;
            return;
        }

        PrettyConsole.Out.MarkupLineInterpolated($"Sent text to the application \"{name}\".");
    }
}
