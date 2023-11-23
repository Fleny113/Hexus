using Hexus.Daemon.Contracts;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

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
        Command.SetHandler(Handle);
    }

    private static async Task Handle(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var text = string.Join(' ', context.ParseResult.GetValueForArgument(InputArgument));
        var newLine = !context.ParseResult.GetValueForOption(DontAddNewLineOption);
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return;
        }

        var stdinRequest = await HttpInvocation.HttpClient.PostAsJsonAsync($"/{name}/stdin", new SendInputRequest(text, newLine), ct);
        
        if (!stdinRequest.IsSuccessStatusCode)
        {
            ErrorResponse? response;
            
            if (stdinRequest is { StatusCode: HttpStatusCode.BadRequest, Content.Headers.ContentType.MediaType: "application/problem+json" })
            {
                var validationResponse = await stdinRequest.Content.ReadFromJsonAsync<ProblemDetails>(HttpInvocation.JsonSerializerOptions, ct);
                
                Debug.Assert(validationResponse is not null);
                
                var errorString = string.Join("\n", validationResponse.Errors.SelectMany(kvp => kvp.Value.Select(v => $"- [tan]{kvp.Key}[/]: {v}")));

                response = new ErrorResponse($"Validation errors: \n{errorString}");
            }
            else
            {
                response = await stdinRequest.Content.ReadFromJsonAsync<ErrorResponse>(HttpInvocation.JsonSerializerOptions, ct);
                response ??= new ErrorResponse("The daemon had an internal server error.");   
            }

            PrettyConsole.Error.MarkupLine($"There [indianred1]was an error[/] creating the application \"{name}\": {response.Error}");
            return;
        }
        
        PrettyConsole.Out.MarkupLine($"Sent text to the application \"{name}\".");
    }
}
