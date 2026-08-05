using Hexus.Configuration;
using Spectre.Console;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tomlyn;

namespace Hexus.Commands.Utils;

internal static partial class MigratePm2Command
{
    private static readonly string[] Pm2KnownConfig =
    [
        "versioning",
        "version",
        "unstable_restarts",
        "restart_time",
        "created_at",
        "axm_dynamic",
        "axm_options",
        "axm_monitor",
        "axm_actions",
        "pm_uptime",
        "status",
        "unique_id",
        "vizion_running",
        "km_link",
        "pm_pid_path",
        "pm_err_log_path",
        "pm_out_log_path",
        "exec_mode",
        "exec_interpreter",
        "pm_cwd",
        "pm_exec_path",
        "node_args",
        "name",
        "filter_env",
        "namespace",
        "args",
        "env",
        "merge_logs",
        "vizion",
        "autorestart",
        "watch",
        "instance_var",
        "pmx",
        "automation",
        "treekill",
        "username",
        "windowsHide",
        "kill_retry_time",
        "exit_code",
    ];

    private static readonly Option<string> Pm2DumpFile = new("--pm2-dump")
    {
        Description = "The pm2 dump file",
        DefaultValueFactory = _ => Path.Combine(HexusPaths.Home, ".pm2", "dump.pm2"),
    };

    public static readonly Command Command = new("migrate-pm2", "Migrate your current PM2 Config to Hexus.") { Pm2DumpFile, };

    static MigratePm2Command()
    {
        Command.SetAction(Handler);
    }

    private static async Task<int> Handler(ParseResult parseResult, CancellationToken ct)
    {
        var pm2Dump = parseResult.GetRequiredValue(Pm2DumpFile);

        PrettyConsole.Error.MarkupLine("[yellow]WARNING[/]: This has been tested with PM2 5.3.0. It might not work with other versions.");

        if (!File.Exists(pm2Dump))
        {
            PrettyConsole.Error.MarkupLineInterpolated(
                $"The specified dump file [indianred1]does not exist[/] ({pm2Dump}). Try using the --pm2-dump option to change the dump file name");
            return 1;
        }

        var pm2ConfigContent = await File.ReadAllTextAsync(pm2Dump, ct);
        var pm2ConfigNode = JsonSerializer.Deserialize<JsonNode>(pm2ConfigContent, Pm2SerializerContext.Default.JsonNode);

        if (pm2ConfigNode is null)
        {
            PrettyConsole.Error.MarkupLine("[indianred1]An error occurred[/] reading the pm2 dump file.");
            return 1;
        }

        List<(string Name, ApplicationConfiguration.ApplicationConfigurationRaw Configuration)> parsedApplications = [];

        try
        {
            foreach (var listNode in pm2ConfigNode.AsArray())
            {
                var appConfig = listNode!.AsObject();

                var execMode = appConfig["exec_mode"]!.GetValue<string>();
                var name = appConfig["name"]!.GetValue<string>();
                var executable = appConfig["pm_exec_path"]?.GetValue<string>();
                var pm2Args = appConfig["args"]?.AsArray();
                var cwd = appConfig["pm_cwd"]?.GetValue<string>();
                var status = appConfig["status"]?.GetValue<string>();

                if (execMode != "fork_mode")
                {
                    PrettyConsole.Error.MarkupLineInterpolated(
                        $"[yellow]WARNING[/]: The application \"{name}\" has been ignore due to exec mode being set to \"{execMode}\". Hexus only supports fork_mode applications");
                    continue;
                }

                var environmentVariables = new Dictionary<string, string>();

                foreach (var (key, value) in appConfig)
                {
                    if (Pm2KnownConfig.Contains(key)) continue;

                    if (value!.GetValueKind() == JsonValueKind.String)
                    {
                        environmentVariables.Add(key, value.GetValue<string>());
                    }
                }

                if (executable is null)
                {
                    PrettyConsole.Error.MarkupLineInterpolated($"[indianred1]Unable to parse application[/] {name} due to missing executable");
                    continue;
                }

                if (cwd is null)
                {
                    PrettyConsole.Error.MarkupLineInterpolated($"[indianred1]Unable to parse application[/] {name} due to missing working directory");
                    continue;
                }

                if (status is null)
                {
                    PrettyConsole.Error.MarkupLineInterpolated($"[indianred1]Unable to parse application[/] {name} due to missing status");
                    continue;
                }

                var args = "";
                var isEnabled = status == "online";

                if (executable.EndsWith(".js"))
                {
                    args = executable;
                    executable = PathHelper.ResolveExecutable("node");
                }

                if (pm2Args is not null)
                {
                    args += string.Join(" ", pm2Args);
                }

                var hexusApplication = new ApplicationConfiguration.ApplicationConfigurationRaw
                {
                    Exe = executable,
                    Args = string.IsNullOrWhiteSpace(args) ? null : args,
                    WorkingDir = cwd,
                    Enabled = isEnabled,
                    Env = environmentVariables,
                    Note = "This application was migrated from a PM2 configuration. Please verify the configuration and make any necessary adjustments.",
                };

                parsedApplications.Add((name, hexusApplication));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("An error occurred parsing the pm2 dump config, see inner exception for details", ex);
        }

        parsedApplications =
        [
            .. parsedApplications.Select(app =>
            {
                var origianlName = app.Name;
                var count = 0;

                while (File.Exists(Path.Combine(HexusPaths.ApplicationsConfigDirectory, $"{app.Name}.toml")))
                {
                    var suffix = count++ is 0 ? "pm2" : $"pm2-{count}";
                    app = app with { Name = $"{origianlName}-{suffix}", };
                }

                return app;
            }),
        ];

        PrettyConsole.Out.MarkupLineInterpolated($"[green]Successfully parsed[/] {parsedApplications.Count} applications from the pm2 dump file");

        HexusPaths.EnsureDirectoriesExistence();

        foreach (var (name, config) in parsedApplications)
        {
            var configPath = Path.Combine(HexusPaths.ApplicationsConfigDirectory, $"{name}.toml");
            await using var configStream = File.OpenWrite(configPath);

            TomlSerializer.Serialize(configStream, config, ConfigurationSerializerContext.Default.ApplicationConfigurationRaw);

            PrettyConsole.Out.MarkupLineInterpolated($"[green]Successfully wrote[/] {configPath}");
        }

        return 0;
    }

    [JsonSerializable(typeof(JsonNode))]
    [JsonSourceGenerationOptions]
    internal partial class Pm2SerializerContext : JsonSerializerContext;
}
