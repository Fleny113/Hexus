using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using Spectre.Console;
using System.CommandLine;
using System.Net.Http.Json;

namespace Hexus.Commands.Applications;

internal static class DeleteCommand
{
    private static readonly Argument<string[]> NamesArgument = new("name")
    {
        Description = "The name(s) of the application(s) to delete. You can specify multiple names separated by spaces.", Arity = ArgumentArity.OneOrMore,
    };

    private static readonly Option<bool> ForceOption = new("--force", "-f") { Description = "Keep the log files of the application(s) after deletion", };
    private static readonly Option<bool> KeepLogFilesOption = new("--keep-logs", "-k") { Description = "Keep the log files of the application(s) after deletion", };

    public static readonly Command Command = new("delete", "Stops and delete an application") { NamesArgument, ForceOption, KeepLogFilesOption, };

    static DeleteCommand()
    {
        Command.SetAction(Handler);
    }

    private static async Task<int> Handler(ParseResult parseResult, CancellationToken ct)
    {
        var names = parseResult.GetRequiredValue(NamesArgument);
        var force = parseResult.GetValue(ForceOption);
        var keepLogs = parseResult.GetValue(KeepLogFilesOption);

        if (names.Length == 0)
        {
            PrettyConsole.Error.MarkupLine("You must specify at least one application name to delete.");
            return 1;
        }

        var exitCode = 0;

        var actOnDaemon = await HttpInvocation.CheckForRunningDaemon(ct) &&
                          await PrettyConsole.Out.ConfirmAsync("The daemon is running. Do you want to stop the applications and then reload the changes?", true, ct);

        foreach (var name in names)
        {
            if (actOnDaemon)
            {
                var stopRequest = await HttpInvocation.DeleteAsync($"Stopping application: {name}", $"/{name}?forceStop={force}", ct);

                if (!stopRequest.IsSuccessStatusCode)
                {
                    await HttpInvocation.HandleFailedHttpRequestLogging(stopRequest, ct);
                    exitCode = 1;
                    continue;
                }
            }

            var configFile = Path.Combine(EnvironmentHelper.ApplicationsConfigDirectory, $"{name}.toml");
            var stateFile = Path.Combine(EnvironmentHelper.ApplicationStatesDirectory, $"{name}.state");
            var logFile = Path.Combine(EnvironmentHelper.ApplicationLogsDirectory, $"{name}.log");

            if (!File.Exists(configFile))
            {
                PrettyConsole.Error.MarkupLineInterpolated($"Application \"{name}\" [darkred_1]does not exist[/].");
                exitCode = 1;
                continue;
            }

            File.Delete(configFile);

            if (File.Exists(stateFile))
                File.Delete(stateFile);

            if (!keepLogs && File.Exists(logFile))
                File.Delete(logFile);

            PrettyConsole.Out.MarkupLineInterpolated($"Application \"{name}\" [darkred_1]deleted[/]!");
        }

        if (!actOnDaemon)
        {
            return 0;
        }

        PrettyConsole.Out.WriteLine();
        PrettyConsole.Out.MarkupLine("Reloading the daemon configuration to apply the changes...");

        var restartRequest = await HttpInvocation.PostAsJsonAsync(
            "Reloading config for the deleted apps", "/daemon/reload", Enumerable.Empty<string>(),
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

        var hasErrors = LogReloadResult(reloadResult);
        if (hasErrors) exitCode = 1;

        return exitCode;
    }

    private static bool LogReloadResult(ReloadResult reloadResult)
    {
        if (reloadResult.Actions.Any())
        {
            PrettyConsole.Out.MarkupLine("Reload with the following actions:");
            foreach (var action in reloadResult.Actions)
            {
                PrettyConsole.Out.MarkupLineInterpolated($" - [blue]{action}[/]");
            }
        }

        if (reloadResult.Warnings.Any())
        {
            PrettyConsole.Out.MarkupLine("Reload completed with warnings:");
            foreach (var warning in reloadResult.Warnings)
            {
                PrettyConsole.Out.MarkupLineInterpolated($" - [yellow]{warning}[/]");
            }
        }

        if (reloadResult.Errors.Any())
        {
            PrettyConsole.Error.MarkupLine("Reload completed with errors:");
            foreach (var error in reloadResult.Errors)
            {
                PrettyConsole.Error.MarkupLineInterpolated($" - [red]{error}[/]");
            }

            return true;
        }

        return false;
    }
}
