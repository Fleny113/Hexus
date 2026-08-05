using Hexus.Configuration;
using Hexus.Extensions;
using Spectre.Console;
using System.CommandLine;
using System.Net.Http.Json;
using Tomlyn;

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
        Description = "The additional arguments for the executable",
        Arity = ArgumentArity.ZeroOrMore,
        DefaultValueFactory = _ => [],
    };

    private static readonly Option<string> WorkingDirOption = new("--working-dir", "-w")
    {
        Description = "Set the current working directory for the application, defaults to the current folder",
        DefaultValueFactory = _ => Environment.CurrentDirectory,
    };

    private static readonly Option<bool> NotEnabledOption = new("--no-enable")
    {
        Description = "Default the application to not be enabled.",
        DefaultValueFactory = _ => false,
    };

    private static readonly Option<Dictionary<string, string>> EnvironmentVariables = new("-e", "--env")
    {
        Description = "Add a environment variables for the application, can be used multiple times. Format: 'key:value' or 'key=value'",
        Arity = ArgumentArity.OneOrMore,
        AllowMultipleArgumentsPerToken = true,
        CustomParser = DictionaryParser.Parse,
        DefaultValueFactory = _ => [],
    };

    public static readonly Command Command = new("new", "Create a new application")
    {
        NameArgument,
        ExecutableArgument,
        ArgumentsArgument,
        WorkingDirOption,
        NotEnabledOption,
        EnvironmentVariables,
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
        var workingDirectory = Path.GetFullPath(parseResult.GetRequiredValue(WorkingDirOption));
        var enabled = !parseResult.GetRequiredValue(NotEnabledOption);
        var environmentVariables = parseResult.GetRequiredValue(EnvironmentVariables);

        HexusPaths.EnsureDirectoriesExistence();
        var configFile = Path.Combine(HexusPaths.ApplicationsConfigDirectory, $"{name}.toml");

        if (File.Exists(configFile))
        {
            PrettyConsole.Error.MarkupLineInterpolated($"Application \"{name}\" already exists. Please delete it first if you want to create a new one.");
            return 1;
        }

        executable = Path.IsPathFullyQualified(executable)
            ? Path.GetFullPath(executable)
            : PathHelper.ResolveExecutable(executable);

        LogPythonWarnIfNecessary(executable, environmentVariables);

        var config = new ApplicationConfiguration.ApplicationConfigurationRaw
        {
            Exe = executable,
            Args = string.IsNullOrWhiteSpace(arguments) ? null : arguments,
            WorkingDir = workingDirectory,
            Enabled = enabled,
            Env = environmentVariables,
        };

        {
            await using var fileStream = File.OpenWrite(configFile);

            {
                using var writer = new StreamWriter(fileStream, leaveOpen: true);

                writer.WriteLine("""
                    # This is a Hexus application configuration file.
                    # For more information about the configuration options, see: https://github.com/Fleny113/Hexus#applicationsnametoml

                    """);
            }

            TomlSerializer.Serialize(fileStream, config, ConfigurationSerializerContext.Default.ApplicationConfigurationRaw);
        }

        PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [palegreen3]created[/]!");

        var confirm = await PrettyConsole.Out.ConfirmAsync("Do you want to edit the application configuration now?", false, ct);

        if (confirm)
        {
            await EditCommand.EditConfiguration(name, ct);
        }

        var actOnDaemon = await HttpInvocation.CheckForRunningDaemon(ct) &&
                          await PrettyConsole.Out.ConfirmAsync("The daemon is running. Do you want to reload the application now?", true, ct);

        if (!actOnDaemon)
        {
            return 0;
        }

        PrettyConsole.Out.WriteLine();
        PrettyConsole.Out.WriteLine("Reloading the daemon configuration to apply the changes...");

        var restartRequest = await HttpInvocation.PostAsJsonAsync<string[]>(
            "Reloading config for the created app", "/daemon/reload", [name],
            HttpInvocation.JsonSerializerContext, ct);

        if (!restartRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(restartRequest, ct);
            return 1;
        }

        var reloadResult = await restartRequest.Content.ReadFromJsonAsync(HttpInvocation.JsonSerializerContext.ReloadResult, ct);

        if (reloadResult is null)
        {
            PrettyConsole.Error.MarkupLine("Failed to parse the reload result.");
            return 1;
        }

        return HttpInvocation.LogReloadResult(reloadResult) ? 1 : 0;
    }

    private static void LogPythonWarnIfNecessary(string executable, Dictionary<string, string> environmentVariables)
    {
        // TODO: Check if implementing PTYs removes the need for stuff like this

        // Python will not send the logs due to buffering the stdout/stderr, since this can look like a bug in Hexus we warn the user
        var fileName = Path.GetFileName(executable);

        // This check can cause false positivies it the exe is not python but starts with "py"
        // However "py" is the longest common string for all python exe(s), including the Windows python launcher, as:
        // - On Windows you get: "py" on WINDIR, "python" on the installation folder
        // - On Linux you get: "python", "python3", "python3.<ver>" based on distro configuration
        var isPython = fileName.StartsWith("py");

        // This only checks for PYTHONUNBUFFERED, checking for -u would be problematic and would require parsing the arguments, something we do not want to do
        var isPyStdoutUnbuffered = environmentVariables.GetValueOrDefault("PYTHONUNBUFFERED") is { Length: > 0 };

        if (!isPython || isPyStdoutUnbuffered)
        {
            return;
        }

        PrettyConsole.Error.MarkupLine("""
            [yellow1]Warning[/]: A python executable was detected. Hexus will not be able to get the output of the program without the '-u' flag or 'PTYHONUNBUFFERED' environment variable. If you are actually running Python, consider using either solution.

            Python documentation for those options: [link]https://docs.python.org/3/using/cmdline.html#cmdoption-u[/]

            [italic]Due to limitations, if you are using the '-u' flag, Hexus will still show this warning. You can ignore it if you are using the '-u' flag.[/]

            If you are not using Python, you can ignore this warning.

            """);
    }
}
