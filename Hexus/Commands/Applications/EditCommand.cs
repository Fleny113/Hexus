using Hexus.Daemon.Contracts.Requests;
using Hexus.Extensions;
using Spectre.Console;
using System.Collections;
using System.CommandLine;

namespace Hexus.Commands.Applications;

internal static class EditCommand
{
    private static readonly Argument<string> NameArgument = new("name")
    {
        Description = "The name(s) of the application(s) to edit"
    };
    private static readonly Option<string> NameOption = new("--name", "-n")
    {
        Description = "The new name for the application"
    };
    private static readonly Option<string> ExecutableOptions = new("--executable", "-x")
    {
        Description = "The new executable for the application"
    };

    private static readonly Option<string[]> ArgumentsOption = new("--arguments", "-a")
    {
        Description = "The new arguments for the application",
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = true,
    };

    private static readonly Option<string> WorkingDirectoryOption = new("--working-directory", "-w")
    {
        Description = "The new working directory for the application",
    };

    private static readonly Option<string> NoteOption = new("--note", "-t")
    {
        Description = "The new note for the application",
    };

    private static readonly Option<bool> ReloadFromShell = new("--reload-from-shell")
    {
        Description = "Use the current shell environment for the application.",
    };

    private static readonly Option<Dictionary<string, string>> AddEnvironmentVariables = new("--environment", "-e")
    {
        Description = "Add an environment variable for the application, format: 'key:value' or 'key=value'",
        Arity = ArgumentArity.OneOrMore,
        AllowMultipleArgumentsPerToken = true,
        CustomParser = DictionaryParser.Parse,
    };

    private static readonly Option<string[]> RemoveEnvironmentVariables = new("--remove-environment", "-r")
    {
        Description = "Remove an environment variable for the application",
        Arity = ArgumentArity.OneOrMore,
        AllowMultipleArgumentsPerToken = true,
    };
    
    private static readonly Option<long?> MemoryLimit = new("-m", "--memory-limit")
    {
        Description = "Set a memory limit for the application in bytes, if the application exceeds this limit it will be restarted. Use 0 to remove the limit and -1 to use the global limit.",
    };

    public static readonly Command Command = new("edit", "Edit an exiting application")
    {
        NameArgument,
        NameOption,
        ExecutableOptions,
        ArgumentsOption,
        NoteOption,
        WorkingDirectoryOption,
        ReloadFromShell,
        AddEnvironmentVariables,
        RemoveEnvironmentVariables,
        MemoryLimit,
    };

    static EditCommand()
    {
        Command.SetAction(Handler);
    }

    private static async Task<int> Handler(ParseResult parseResult, CancellationToken ct)
    {
        var name = parseResult.GetRequiredValue(NameArgument);
        var newName = parseResult.GetValue(NameOption);
        var newExecutable = parseResult.GetValue(ExecutableOptions);
        var newArgumentsOptionValue = parseResult.GetValue(ArgumentsOption);
        var newWorkingDirectory = parseResult.GetValue(WorkingDirectoryOption);
        var newNote = parseResult.GetValue(NoteOption);
        var addEnv = parseResult.GetValue(AddEnvironmentVariables);
        var remove = parseResult.GetValue(RemoveEnvironmentVariables);
        var reloadEnv = parseResult.GetValue(ReloadFromShell);
        var memoryLimit = parseResult.GetValue(MemoryLimit);

        var newArguments = string.Join(' ', newArgumentsOptionValue ?? []);

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return 1;
        }

        if (newWorkingDirectory is not null)
            newWorkingDirectory = Path.GetFullPath(newWorkingDirectory);

        if (newExecutable is not null)
            newExecutable = Path.IsPathFullyQualified(newExecutable)
                ? Path.GetFullPath(newExecutable)
                : PathHelper.ResolveExecutable(newExecutable);

        if (reloadEnv)
        {
            addEnv ??= [];

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

        var editRequest = await HttpInvocation.PatchAsJsonAsync(
            "Editing application",
            $"{name}",
            new EditApplicationRequest(
                Name: newName,
                Executable: newExecutable,
                Arguments: newArgumentsOptionValue is { Length: 0 }
                    ? null
                    : newArguments,
                WorkingDirectory: newWorkingDirectory,
                Note: newNote,
                NewEnvironmentVariables: addEnv,
                RemoveEnvironmentVariables: remove,
                IsReloadingEnvironmentVariables: reloadEnv,
                MemoryLimit: memoryLimit
            ),
            HttpInvocation.JsonSerializerContext,
            ct
        );

        if (!editRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(editRequest, ct);
            return 1;
        }

        PrettyConsole.Out.MarkupLineInterpolated(
            $"Application \"{name}\" [plum2]edited[/]. You can now run it with the '[darkcyan]start[/] [cornflowerblue]{newName ?? name}[/]' command"
        );

        return 0;
    }
}
