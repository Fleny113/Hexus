using Hexus.Daemon.Configuration;
using Spectre.Console;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Hexus.Commands.Utils;

internal static partial class MigratePm2Command
{
    private static readonly string[] Pm2KnownConfig = [
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
        DefaultValueFactory = _ => Path.GetFullPath($"{EnvironmentHelper.Home}/.pm2/dump.pm2"),
    };

    public static readonly Command Command = new("migrate-pm2", "Migrate your current PM2 Config to Hexus.")
    {
        Pm2DumpFile,
    };

    static MigratePm2Command()
    {
        Command.SetAction(Handler);
    }

    private static async Task<int> Handler(ParseResult parseResult, CancellationToken ct)
    {
        var pm2Dump = parseResult.GetRequiredValue(Pm2DumpFile);

        PrettyConsole.Error.MarkupLine("[yellow]WARNING[/]: This has been tested with PM2 5.3.0. It might not work with other versions.");

        if (await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine("To edit the Hexus configuration the [indianred1]daemon needs to not be running[/]. Stop it first using the '[indianred1]daemon[/] [darkseagreen1_1]stop[/]' command.");
            return 1;
        }

        if (!File.Exists(pm2Dump))
        {
            PrettyConsole.Error.MarkupLineInterpolated($"The specified dump file [indianred1]does not exist[/]. {pm2Dump} does not exist. Try using the --pm2-dump option to change the dump file name");
            return 1;
        }

        var pm2ConfigContent = await File.ReadAllTextAsync(pm2Dump, ct);
        var pm2ConfigNode = JsonSerializer.Deserialize<JsonNode>(pm2ConfigContent, Pm2SerializerContext.Default.JsonNode);

        if (pm2ConfigNode is null)
        {
            PrettyConsole.Error.MarkupLine("There [indianred1]was an error[/] reading the pm2 dump file.");
            return 1;
        }

        List<HexusApplication> parsedApplications = [];

        try
        {
            foreach (var listNode in pm2ConfigNode.AsArray())
            {
                var appConfig = listNode!.AsObject();

                var execMode = appConfig["exec_mode"]!.GetValue<string>();
                var name = appConfig["name"]!.GetValue<string>();

                if (execMode != "fork_mode")
                {
                    PrettyConsole.Error.MarkupLineInterpolated($"[yellow]WARNING[/]: The application \"{name}\" where the exec mode is set to \"{execMode}\" has been ignored as Hexus supports fork_mode applications only");
                    continue;
                }

                var hexusApplication = new HexusApplication { Name = name, Executable = null! };

                foreach (var (key, value) in appConfig)
                {
                    if (!Pm2KnownConfig.Contains(key))
                    {
                        if (value!.GetValueKind() == JsonValueKind.String)
                        {
                            hexusApplication.EnvironmentVariables.Add(key, value.GetValue<string>());
                        }
                    }

                    switch (key)
                    {
                        case "pm_exec_path":
                            hexusApplication.Executable = value!.GetValue<string>();

                            if (hexusApplication.Executable.EndsWith(".js"))
                            {
                                hexusApplication.Arguments = $"{hexusApplication.Executable} {hexusApplication.Arguments}";
                                hexusApplication.Executable = PathHelper.ResolveExecutable("node");
                            }

                            break;
                        case "args":
                            var joinedArgs = string.Join(" ", value!.AsArray());
                            hexusApplication.Arguments += joinedArgs;
                            break;
                        case "pm_cwd":
                            hexusApplication.WorkingDirectory = value!.GetValue<string>();
                            break;
                        case "status":
                            hexusApplication.Status = value!.GetValue<string>() switch
                            {
                                "errored" => HexusApplicationStatus.Crashed,
                                "stopped" => HexusApplicationStatus.Exited,
                                "online" => HexusApplicationStatus.Running,
                                _ => HexusApplicationStatus.Exited,
                            };

                            break;
                    }
                }

                parsedApplications.Add(hexusApplication);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("There was an error parsing the pm2 dump config, see inner exception for details", ex);
        }

        foreach (var application in parsedApplications)
        {
            // To avoid most conflicts we add a -pm2 suffix if the key already exists.
            if (Configuration.HexusConfiguration.Applications.ContainsKey(application.Name))
            {
                application.Name += "-pm2";
            }

            if (!Configuration.HexusConfiguration.Applications.TryAdd(application.Name, application))
            {
                PrettyConsole.Error.MarkupLineInterpolated($"[indianred1]Unable to add application[/] {application.Name} due to conflicts with already exiting applications");
            }
        }

        Configuration.HexusConfigurationManager.SaveConfiguration();

        return 0;
    }

    [JsonSerializable(typeof(JsonNode))]
    [JsonSourceGenerationOptions]
    internal partial class Pm2SerializerContext : JsonSerializerContext;
}
