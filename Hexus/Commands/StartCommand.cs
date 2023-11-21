using Hexus.Daemon.Contracts;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;

namespace Hexus.Commands;

internal static class StartCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application to start");

    public static readonly Command Command = new("start", "Start an exiting application")
    {
        NameArgument,
    };

    static StartCommand()
    {
        Command.SetHandler(HandleAsync);
    }

    private static async Task HandleAsync(InvocationContext context)
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
            var response = await startRequest.Content.ReadFromJsonAsync<ErrorResponse>(HttpInvocation.JsonSerializerOptions, ct);

            response ??= new ErrorResponse("The daemon had an internal server error.");

            PrettyConsole.Error.MarkupLine($"There [indianred1]was an error[/] starting the application \"{name}\": {response.Error}");
            return;
        }

        PrettyConsole.Out.MarkupLine($"Application \"{name}\" [darkseagreen1_1]started[/]!");
    }
}
