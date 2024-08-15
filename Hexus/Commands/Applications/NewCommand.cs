using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Extensions;
using Spectre.Console;
using System.Collections;
using System.CommandLine;
using System.CommandLine.Invocation;

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
            : PathHelper.ResolveExecutable(executable);

        // Python will not send the logs due to buffering the stdout/stderr, since this can look like a bug in Hexus we warn the user

        var fileName = Path.GetFileName(executable);
        // This check can cause false positivies it the exe is not python but starts with "py"
        // However "py" is the smallest common string for all python exe(s), including the Windows python launcher, as:
        // - On Windows you get: "py" on WINDIR, "python" on the install folder
        // - On Linux you get: "python", "python3", "python3.<ver>" based on distro configuration
        var isPython = fileName.StartsWith("py");

        // This only checks for PYTHONUNBUFFERED, checking for -u would be problematic and would require parsing the arguments, something we do not want to do
        var isPyStdoutUnbuffered = environmentVariables.TryGetValue("PYTHONUNBUFFERED", out var pyUnbuffered) && pyUnbuffered?.Length > 0;

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
}
