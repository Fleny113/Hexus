
using Hexus.Configuration;
using Spectre.Console;
using System.CommandLine;
using Tomlyn;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hexus.Commands.Utils;

internal static class MigrateConfigCommand
{
    public static readonly Command Command = new("migrate-config", "Migrate the old .yaml configuration to new .toml configugration");

    static MigrateConfigCommand()
    {
        Command.SetAction(Handler);
    }

    private static async Task<int> Handler(ParseResult parseResult)
    {
        var configFile = Path.Combine(HexusPaths.XdgConfig, $"hexus{HexusPaths.FileSuffix}.yaml");

        if (!File.Exists(configFile))
        {
            PrettyConsole.Error.MarkupLine($"[red]The old configuration file ({configFile}) does not exist.[/]");
            return 1;
        }

        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var text = await File.ReadAllTextAsync(configFile);
        var oldConfig = yamlDeserializer.Deserialize<HexusConfigurationFile>(text);

        var newDaemonConfig = new DaemonConfiguration.DaemonConfigurationRaw()
        {
            UnixSocket = oldConfig.UnixSocket,
            HttpPort = oldConfig.HttpPort,
            CpuPollingInterval = oldConfig.CpuRefreshIntervalSeconds is null ? null : $"{oldConfig.CpuRefreshIntervalSeconds}s",
            MemoryPollingInterval = oldConfig.MemoryLimitCheckIntervalSeconds is null ? null : $"{oldConfig.MemoryLimitCheckIntervalSeconds}s",
            MemoryLimit = oldConfig.MemoryLimit is null ? null : $"{oldConfig.MemoryLimit}B",
        };

        List<(string Name, ApplicationConfiguration.ApplicationConfigurationRaw Config)> newApplications =
        [
            .. oldConfig.Applications?.Select(kv => (Name: kv.Key, Config: new ApplicationConfiguration.ApplicationConfigurationRaw
            {
                Exe = kv.Value.Executable,
                Args = kv.Value.Arguments,
                WorkingDir = kv.Value.WorkingDirectory,
                Environment = kv.Value.EnvironmentVariables,
                MemoryLimit = kv.Value.MemoryLimit is null ? null : $"{kv.Value.MemoryLimit}B",
                Enabled = kv.Value.Status == HexusApplicationStatus.Running,
                Note = kv.Value.Note,
            })) ?? [],
        ];

        PrettyConsole.Out.MarkupLineInterpolated($"[green]Successfully parsed[/] {newApplications.Count} applications from the hexus yaml config file");

        HexusPaths.EnsureDirectoriesExistence();

        var confirm = true;

        if (File.Exists(HexusPaths.DaemonConfigFile))
        {
            confirm = PrettyConsole.Out.Confirm("The deamon configuration already exists. Do you want to overwrite it?", false);
        }

        if (!confirm)
        {
            PrettyConsole.Out.MarkupLineInterpolated($"[yellow]Skipping[/] daemon configuration");
        }
        else
        {

            var config = TomlSerializer.Serialize(newDaemonConfig, ConfigurationSerializerContext.Default.DaemonConfigurationRaw);

            if (string.IsNullOrWhiteSpace(config))
            {
                PrettyConsole.Error.MarkupLine($"[blue]Note[/]: The daemon configuration is default, skipping writing it to {HexusPaths.DaemonConfigFile}");
            }
            else
            {
                await File.WriteAllTextAsync(HexusPaths.DaemonConfigFile, config);

                PrettyConsole.Out.MarkupLineInterpolated($"[green]Successfully wrote[/] daemon config at {HexusPaths.DaemonConfigFile}");
            }

        }

        foreach (var (name, config) in newApplications)
        {
            var configPath = Path.Combine(HexusPaths.ApplicationsConfigDirectory, $"{name}.toml");

            if (File.Exists(configPath))
            {
                var appConfirm = PrettyConsole.Out.Confirm($"The application config file already exists at {configPath}. Do you want to overwrite it?", false);

                if (!appConfirm)
                {
                    PrettyConsole.Out.MarkupLineInterpolated($"[yellow]Skipping[/] {configPath}");
                    continue;
                }
            }

            await using var configStream = File.OpenWrite(configPath);

            TomlSerializer.Serialize(configStream, config, ConfigurationSerializerContext.Default.ApplicationConfigurationRaw);

            PrettyConsole.Out.MarkupLineInterpolated($"[green]Successfully wrote[/] {configPath}");
        }

        return 0;
    }


    private sealed record HexusApplication
    {
        public string Executable { get; set; } = null!;

        public string? Arguments { get; set; }
        public string WorkingDirectory { get; set; } = null!;
        public HexusApplicationStatus Status { get; set; } = HexusApplicationStatus.Exited;
        public string? Note { get; set; }

        public Dictionary<string, string> EnvironmentVariables { get; set; } = [];

        public long? MemoryLimit { get; set; }
    }

    private sealed record HexusConfigurationFile
    {
        public string? UnixSocket { get; set; }
        public int? HttpPort { get; set; }
        public double? CpuRefreshIntervalSeconds { get; set; }
        public double? MemoryLimitCheckIntervalSeconds { get; set; }
        public long? MemoryLimit { get; set; }
        public Dictionary<string, HexusApplication>? Applications { get; set; }
    }

    private enum HexusApplicationStatus
    {
        Crashed = -2,
        Exited = -1,
        Running = 0,

        Restarting = 1,
        Stopping = 2,
    }
}
