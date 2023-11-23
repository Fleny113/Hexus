using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;

namespace Hexus.Commands.Applications;

internal static class EditCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application to edit");
    private static readonly Option<string> NameOption = new(["-n", "--name"], "The new name for the application");
    private static readonly Option<string> ExecutableOptions = new(["-e", "--executable"], "The new executable for the application");
    private static readonly Option<string[]> ArgumentsOption = new(["-a", "--arguments"], "The new arguments for the application")
    {
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = true,
    };
    private static readonly Option<string> WorkingDirectoryOption = new(["-w", "--working-directory"], "The new working directory for the application");
    
    public static readonly Command Command = new("edit", "Edit an exiting application")
    {
        NameArgument,
        NameOption,
        ExecutableOptions,
        ArgumentsOption,
        WorkingDirectoryOption,
    };

    static EditCommand()
    {
        Command.SetHandler(Handle);
    }

    private static async Task Handle(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var newName = context.ParseResult.GetValueForOption(NameOption);
        var newExecutable = context.ParseResult.GetValueForOption(ExecutableOptions);
        var newArgumentsOptionValue = context.ParseResult.GetValueForOption(ArgumentsOption);
        var newWorkingDirectory = context.ParseResult.GetValueForOption(WorkingDirectoryOption);
        var ct = context.GetCancellationToken();

        var newArguments = string.Join(' ', newArgumentsOptionValue ?? Array.Empty<string>());

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return;
        }

        if (newWorkingDirectory is not null)
            newWorkingDirectory = EnvironmentHelper.NormalizePath(newWorkingDirectory);
        
        if (newExecutable is not null)
            newExecutable = Path.IsPathFullyQualified(newExecutable) 
                ? EnvironmentHelper.NormalizePath(newExecutable) 
                : NewCommand.TryResolveExecutable(newExecutable);
        
        var editRequest = await HttpInvocation.HttpClient.PatchAsJsonAsync(
            $"{name}",
            new EditApplicationRequest(
                newName,
                newExecutable,
                newArgumentsOptionValue is null
                    ? null
                    : newArguments,
                newWorkingDirectory
            ),
            ct
        );

        if (!editRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(editRequest, ct);
            return;
        }
        
        PrettyConsole.Out.MarkupLineInterpolated(
            $"Application \"{name}\" edited. You can now run it with the '[darkcyan]start[/] [cornflowerblue]{newName ?? name}[/]' command"
        );
    }
}
