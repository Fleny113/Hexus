using Hexus.Daemon.Contracts;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net.Http.Json;

namespace Hexus.Commands;

internal static class StopCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application to stop");
    private static readonly Option<bool> ForceOption = new(["--force", "-f"], "Force the stop of the application");
    
    public static readonly Command Command = new("stop", "Stop an application")
    {
        NameArgument,
        ForceOption,
    };

    static StopCommand()
    {        
        Command.SetHandler(HandleAsync);
    }
    
    private static async Task HandleAsync(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var force = context.ParseResult.GetValueForOption(ForceOption);
        var ct = context.GetCancellationToken();

        var stopRequest = await HttpInvocation.HttpClient.DeleteAsync($"/{name}?forceStop={force}", ct);

        if (!stopRequest.IsSuccessStatusCode)
        {
            var response = await stopRequest.Content.ReadFromJsonAsync<ErrorResponse>(ct);

            Debug.Assert(response is not null);

            Console.Error.WriteLine($"There was an error stopping the application \"{name}\" for the following reason: {response.Error}");
        }

        Console.WriteLine($"Application \"{name}\" stopped!");
    }
}
