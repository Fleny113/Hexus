using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Extensions;
using Spectre.Console;
using System.Collections;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;

namespace Hexus.Commands.Applications;

internal static class EditCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application to edit");
    private static readonly Option<string> NameOption = new(["-n", "--name"], "The new name for the application");
    private static readonly Option<string> ExecutableOptions = new(["-x", "--executable"], "The new executable for the application");
    private static readonly Option<string[]> ArgumentsOption = new(["-a", "--arguments"], "The new arguments for the application")
    {
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = true,
    };
    private static readonly Option<string> WorkingDirectoryOption = new(["-w", "--working-directory"], "The new working directory for the application");
    private static readonly Option<bool> ReloadFromShell = 
        new("--reload-from-shell", "Use the current shell environment for the application");
    private static readonly Option<Dictionary<string, string>> AddEnvironmentVariables = 
        new(["-e", "--environment"], "Add an environment variable for the application, format: 'key:value' or 'key=value'")
        {
            Arity = ArgumentArity.OneOrMore, 
            AllowMultipleArgumentsPerToken = true,
        };
    private static readonly Option<string[]> RemoveEnvironmentVariables = 
        new(["-r", "--remove-environment"], "Remove an environment variable for the application")
        {
            Arity = ArgumentArity.OneOrMore, 
            AllowMultipleArgumentsPerToken = true,
        };
    
    public static readonly Command Command = new("edit", "Edit an exiting application")
    {
        NameArgument,
        NameOption,
        ExecutableOptions,
        ArgumentsOption,
        WorkingDirectoryOption,
        ReloadFromShell,
        AddEnvironmentVariables,
        RemoveEnvironmentVariables,
    };

    static EditCommand()
    {
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var binder = new DictionaryBinder(AddEnvironmentVariables);
        
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var newName = context.ParseResult.GetValueForOption(NameOption);
        var newExecutable = context.ParseResult.GetValueForOption(ExecutableOptions);
        var newArgumentsOptionValue = context.ParseResult.GetValueForOption(ArgumentsOption);
        var newWorkingDirectory = context.ParseResult.GetValueForOption(WorkingDirectoryOption);
        var addEnv = context.BindingContext.GetValueForBinder(binder);
        var remove = context.ParseResult.GetValueForOption(RemoveEnvironmentVariables);
        var reloadEnv = context.ParseResult.GetValueForOption(ReloadFromShell);
        var ct = context.GetCancellationToken();

        var newArguments = string.Join(' ', newArgumentsOptionValue ?? []);

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            context.ExitCode = 1;
            return;
        }

        if (newWorkingDirectory is not null)
            newWorkingDirectory = EnvironmentHelper.NormalizePath(newWorkingDirectory);
        
        if (newExecutable is not null)
            newExecutable = Path.IsPathFullyQualified(newExecutable) 
                ? EnvironmentHelper.NormalizePath(newExecutable) 
                : NewCommand.TryResolveExecutable(newExecutable);

        if (reloadEnv)
        {
            addEnv ??= new Dictionary<string, string>();

            foreach (var env in Environment.GetEnvironmentVariables())
            {
                if (env is not DictionaryEntry dictEntry)
                    continue;

                var key = (string)dictEntry.Key;
                var value = (string?)dictEntry.Value;

                if (value is null)
                    continue;
                
                addEnv.TryAdd(key, value);
            }
        }
        
        var editRequest = await HttpInvocation.HttpClient.PatchAsJsonAsync(
            $"{name}",
            new EditApplicationRequest(
                newName,
                newExecutable,
                newArgumentsOptionValue is null
                    ? null
                    : newArguments,
                newWorkingDirectory,
                addEnv,
                remove,
                reloadEnv
            ),
            ct
        );

        if (!editRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(editRequest, ct);
            context.ExitCode = 1;
            return;
        }
        
        PrettyConsole.Out.MarkupLineInterpolated(
            $"Application \"{name}\" [plum2]edited[/]. You can now run it with the '[darkcyan]start[/] [cornflowerblue]{newName ?? name}[/]' command"
        );
    }
}