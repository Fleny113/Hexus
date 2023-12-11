using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Extensions;
using Spectre.Console;
using System.Collections;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;

namespace Hexus.Commands.Applications;

internal static class NewCommand
{
    private static readonly Argument<string> NameArgument =
        new("name", "The name for the application");
    private static readonly Argument<string> ExecutableArgument =
        new("executable", "The file to execute, can resolved through the PATH env") { Arity = ArgumentArity.ExactlyOne };
    private static readonly Argument<string[]> ArgumentsArgument =
        new("arguments", "The additional argument for the executable") { Arity = ArgumentArity.ZeroOrMore };
    private static readonly Option<string> WorkingDirectoryOption =
        new(["-w", "--working-directory"], "Set the current working directory for the application, defaults to the current folder");
    private static readonly Option<string?> NoteOption =
    new(["-n", "--note"], "Set an optional note for this application");
    private static readonly Option<bool> DoNotUseShellEnvironment =
        new("--do-not-use-shell-env", "Don't use the current shell environment for the application");
    private static readonly Option<Dictionary<string, string>> EnvironmentVariables =
        new(["-e", "--environment"], "Add an environment variable for the application, format: 'key:value' or 'key=value'")
        {
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = true,
        };

    public static readonly Command Command = new("new", "Create a new application")
    {
        NameArgument,
        ExecutableArgument,
        ArgumentsArgument,
        NoteOption,
        WorkingDirectoryOption,
        DoNotUseShellEnvironment,
        EnvironmentVariables,
    };

    static NewCommand()
    {
        ArgumentsArgument.SetDefaultValue("");
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var binder = new DictionaryBinder(EnvironmentVariables);

        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var executable = context.ParseResult.GetValueForArgument(ExecutableArgument);
        var arguments = string.Join(' ', context.ParseResult.GetValueForArgument(ArgumentsArgument));
        var note = context.ParseResult.GetValueForOption(NoteOption);
        var workingDirectory = context.ParseResult.GetValueForOption(WorkingDirectoryOption);
        var useShellEnv = !context.ParseResult.GetValueForOption(DoNotUseShellEnvironment);
        var environmentVariables = context.BindingContext.GetValueForBinder(binder) ?? [];
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            context.ExitCode = 1;
            return;
        }

        workingDirectory ??= Environment.CurrentDirectory;
        workingDirectory = EnvironmentHelper.NormalizePath(workingDirectory);

        if (useShellEnv)
        {
            foreach (var env in Environment.GetEnvironmentVariables())
            {
                if (env is not DictionaryEntry dictEntry)
                    continue;

                var key = (string)dictEntry.Key;
                var value = (string?)dictEntry.Value;

                if (value is null)
                    continue;

                environmentVariables.TryAdd(key, value);
            }
        }

        executable = Path.IsPathFullyQualified(executable)
            ? EnvironmentHelper.NormalizePath(executable)
            : TryResolveExecutable(executable);

        var newRequest = await HttpInvocation.HttpClient.PostAsJsonAsync(
            "/new",
            new NewApplicationRequest(
                name,
                executable,
                arguments,
                workingDirectory,
                note ?? "",
                environmentVariables
            ),
            HttpInvocation.JsonSerializerOptions,
            ct
        );

        if (!newRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(newRequest, ct);
            context.ExitCode = 1;
            return;
        }

        PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [palegreen3]created[/]!");
    }

    internal static string TryResolveExecutable(string executable)
    {
        // relative folders resolver (./.../exe)
        var absolutePath = EnvironmentHelper.NormalizePath(executable);

        if (File.Exists(absolutePath))
            return absolutePath;

        // PATH env resolver
        if (executable.Contains('/') || executable.Contains('\\'))
            throw new Exception("Executable cannot have slashes");

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? throw new Exception("Cannot get PATH environment variable");

        // Linux and Windows use different split char for the path
        var paths = pathEnv.Split(OperatingSystem.IsWindows() ? ';' : ':');

        if (OperatingSystem.IsWindows() && !executable.EndsWith(".exe"))
            executable = $"{executable}.exe";

        var resolvedExecutable = paths
            .Select(path => Path.Combine(path, executable))
            .Where(File.Exists)
            .Select(EnvironmentHelper.NormalizePath)
            .FirstOrDefault();

        if (resolvedExecutable is not null)
            return resolvedExecutable;

        // No executable found
        throw new FileNotFoundException("Cannot find the executable");
    }
}
