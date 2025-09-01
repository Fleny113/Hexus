using Hexus.Daemon.Contracts.Requests;
using Hexus.Extensions;
using Spectre.Console;
using System.Collections;
using System.CommandLine;

namespace Hexus.Commands.Applications;

internal static class NewCommand
{
    private static readonly Argument<string> NameArgument = new("name")
    {
        Description = "The name for the application",
    };

    private static readonly Argument<string> ExecutableArgument = new("executable")
    {
        Description = "The file to execute, can resolved through the PATH env",
        Arity = ArgumentArity.ExactlyOne,
    };

    private static readonly Argument<string[]> ArgumentsArgument = new("arguments")
    {
        Description = "The additional argument for the executable",
        Arity = ArgumentArity.ZeroOrMore,
        DefaultValueFactory = _ => [],
    };

    private static readonly Option<string> WorkingDirectoryOption = new("--working-directory", "-w")
    {
        Description = "Set the current working directory for the application, defaults to the current folder",
    };

    private static readonly Option<string?> NoteOption = new("--note", "-n")
    {
        Description = "Set an optional note for this application",
    };

    private static readonly Option<bool> DoNotUseShellEnvironment = new("--do-not-use-shell-env")
    {
        Description = "Don't use the current shell environment for the application",
    };

    private static readonly Option<Dictionary<string, string>> EnvironmentVariables = new("-e", "--environment")
    {
        Description = "Add an environment variable for the application, format: 'key:value' or 'key=value'",
        Arity = ArgumentArity.OneOrMore,
        AllowMultipleArgumentsPerToken = true,
        CustomParser = DictionaryParser.Parse,
    };
    
    private static readonly Option<long?> MemoryLimit = new("-m", "--memory-limit")
    {
        Description = "Set a memory limit for the application in bytes, if the application exceeds this limit it will be restarted",
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
        MemoryLimit,
    };

    static NewCommand()
    {
        Command.SetAction(Handler);
    }

    private static async Task<int> Handler(ParseResult parseResult, CancellationToken ct)
    {
        var name = parseResult.GetRequiredValue(NameArgument);
        var executable = parseResult.GetRequiredValue(ExecutableArgument);
        var arguments = string.Join(' ', parseResult.GetRequiredValue(ArgumentsArgument));
        var note = parseResult.GetValue(NoteOption);
        var workingDirectory = parseResult.GetValue(WorkingDirectoryOption);
        var useShellEnv = !parseResult.GetValue(DoNotUseShellEnvironment);
        var environmentVariables = parseResult.GetValue(EnvironmentVariables) ?? [];
        var memoryLimit = parseResult.GetValue(MemoryLimit);

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return 1;
        }

        workingDirectory ??= Environment.CurrentDirectory;
        workingDirectory = Path.GetFullPath(workingDirectory);

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
            ? Path.GetFullPath(executable)
            : PathHelper.ResolveExecutable(executable);

        // Python will not send the logs due to buffering the stdout/stderr, since this can look like a bug in Hexus we warn the user

        var fileName = Path.GetFileName(executable);

        // This check can cause false positivies it the exe is not python but starts with "py"
        // However "py" is the longest common string for all python exe(s), including the Windows python launcher, as:
        // - On Windows you get: "py" on WINDIR, "python" on the installation folder
        // - On Linux you get: "python", "python3", "python3.<ver>" based on distro configuration
        var isPython = fileName.StartsWith("py");

        // This only checks for PYTHONUNBUFFERED, checking for -u would be problematic and would require parsing the arguments, something we do not want to do
        var isPyStdoutUnbuffered = environmentVariables.TryGetValue("PYTHONUNBUFFERED", out var pyUnbuffered) && pyUnbuffered.Length > 0;

        if (isPython && !isPyStdoutUnbuffered)
        {
            PrettyConsole.Error.MarkupLine("""
                [yellow1]Warning[/]: A python executable was detected. Hexus will not be able to get the output of the program without the '-u' flag or 'PTYHONUNBUFFERED' environment variable. If you are actually running Python, consider using either solutions.

                Python documentation for those options: [link]https://docs.python.org/3/using/cmdline.html#cmdoption-u[/]

                [italic]Due to limitations, if you are using the '-u' flag, Hexus will still show this warning, you can ignore it if you are using the '-u' flag.[/]

                If you are not using Python, you can ignore this warning.

                """);
        }

        var newRequest = await HttpInvocation.PostAsJsonAsync(
            "Creating new application",
            "/new",
            new NewApplicationRequest(
                name,
                executable,
                arguments,
                workingDirectory,
                note ?? "",
                environmentVariables,
                memoryLimit
            ),
            HttpInvocation.JsonSerializerContext,
            ct
        );

        if (!newRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(newRequest, ct);
            return 1;
        }

        PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [palegreen3]created[/]!");

        return 0;
    }
}
