using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Tomlyn;

namespace Hexus.Configuration;

public sealed partial class HexusConfigurationManager
{
    private const string DaemonSource = "Hexus Daemon";

    private static readonly Lock _lock = new();

    public DaemonConfiguration DaemonConfiguration
    {
        get
        {
            using var _ = _lock.EnterScope();
            return field;
        }
        private set;
    }

    public Dictionary<string, ApplicationConfiguration> Applications
    {
        get
        {
            using var _ = _lock.EnterScope();
            return field;
        }
        private set;
    }

    public IEnumerable<ConfigurationNotice> Warnings
    {
        get
        {
            using var _ = _lock.EnterScope();
            return field;
        }
        private set;
    }

    public IEnumerable<ConfigurationNotice> Errors
    {
        get
        {
            using var _ = _lock.EnterScope();
            return field;
        }
        private set;
    }

    public HexusConfigurationManager() => Reload();

    [MemberNotNull(nameof(DaemonConfiguration), nameof(Applications), nameof(Warnings), nameof(Errors))]
    public ConfigurationProblems Reload()
    {
        using var _ = _lock.EnterScope();

        HexusPaths.EnsureDirectoriesExistence();

        var result = LoadDaemonConfiguration();
        DaemonConfiguration = result.Configuration;
        Warnings = result.Warnings;
        Errors = result.Errors;

        Applications = [];

        if (Directory.Exists(HexusPaths.ApplicationsConfigDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(HexusPaths.ApplicationsConfigDirectory, "*.toml"))
            {
                var appName = Path.GetFileNameWithoutExtension(file);
                var appResult = Reload(appName);

                Warnings = Warnings.Concat(appResult.Warnings);
                Errors = Errors.Concat(appResult.Errors);
            }
        }

        return new(Warnings, Errors);
    }

    public ConfigurationProblems Reload(string applicationName)
    {
        using var _ = _lock.EnterScope();

        var result = LoadApplicationConfiguration(applicationName);

        if (result.Configuration is not null)
        {
            Applications[applicationName] = result.Configuration;
        }
        else
        {
            Applications.Remove(applicationName);
        }

        return new(result.Warnings, result.Errors);
    }

    public static ConfigurationLoadResult<DaemonConfiguration> LoadDaemonConfiguration()
    {
        if (!File.Exists(HexusPaths.DaemonConfigFile))
        {
            return ResolveDaemonConfig(null);
        }

        using var file = File.OpenRead(HexusPaths.DaemonConfigFile);
        if (!TomlSerializer.TryDeserialize<DaemonConfiguration.DaemonConfigurationRaw>(file, ConfigurationSerializerContext.Default, out var config))
        {
            var defaultConfig = ResolveDaemonConfig(null);
            return defaultConfig with { Errors = defaultConfig.Errors.Concat([new ConfigurationNotice("Failed to parse daemon configuration file.", DaemonSource)]) };
        }

        return ResolveDaemonConfig(config);
    }

    private ConfigurationLoadResult<ApplicationConfiguration?> LoadApplicationConfiguration(string applicationName)
    {
        if (!File.Exists($"{HexusPaths.ApplicationsConfigDirectory}/{applicationName}.toml"))
        {
            return new(null, [], [new ConfigurationNotice("Configuration file does not exist.", applicationName)]);
        }

        using var file = File.OpenRead($"{HexusPaths.ApplicationsConfigDirectory}/{applicationName}.toml");

        if (!TomlSerializer.TryDeserialize<ApplicationConfiguration.ApplicationConfigurationRaw>(file, ConfigurationSerializerContext.Default, out var config))
        {
            return new(null, [], [new ConfigurationNotice("Failed to parse configuration file.", applicationName)]);
        }

        return ResolveApplicationConfig(config, applicationName);
    }

    private static ConfigurationLoadResult<DaemonConfiguration> ResolveDaemonConfig(DaemonConfiguration.DaemonConfigurationRaw? raw)
    {
        var defaultMemoryLimit = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 4;

        var warnings = new List<ConfigurationNotice>();
        var config = new DaemonConfiguration
        {
            UnixSocket = ResolveSocketPath(raw?.UnixSocket, HexusPaths.DefaultSocketFile, DaemonSource, warnings),
            HttpPort = raw?.HttpPort,
            CpuPollingInterval = ResolveTimeSpan(raw?.CpuPollingInterval, TimeSpan.FromSeconds(2.5), DaemonSource, "cpu-polling-interval", warnings),
            MemoryPollingInterval = ResolveTimeSpan(raw?.MemoryPollingInterval, TimeSpan.FromSeconds(10), DaemonSource, "memory-polling-interval", warnings),
            MemoryLimit = ResolveByteSize(raw?.MemoryLimit, defaultMemoryLimit, DaemonSource, "memory-limit", warnings),
        };

        return new(config, warnings, []);
    }

    private ConfigurationLoadResult<ApplicationConfiguration?> ResolveApplicationConfig(ApplicationConfiguration.ApplicationConfigurationRaw raw, string name)
    {
        var warnings = new List<ConfigurationNotice>();

        var config = new ApplicationConfiguration
        {
            Name = name,
            Executable = raw.Exe,
            Arguments = raw.Args,
            WorkingDirectory = raw.WorkingDir,
            Enabled = raw.Enabled,
            Note = raw.Note,
            EnvironmentVariables = raw.Environment?.ToImmutableDictionary() ?? [],
            MemoryLimit = ResolveByteSize(raw.MemoryLimit, DaemonConfiguration.MemoryLimit, name, "memory-limit", warnings),
        };

        return new(config, warnings, []);
    }

    // Resolvers

    private static TimeSpan ResolveTimeSpan(string? value, TimeSpan defaultValue, string source, string propName, List<ConfigurationNotice> warnings)
    {
        if (value is null) return defaultValue;

        if (!TryParseTimeSpan(value, out var result))
        {
            warnings.Add(new ConfigurationNotice($"Invalid value for {propName}: {value}. Using default value.", source));
            return defaultValue;
        }

        return result;
    }

    private static long ResolveByteSize(string? value, long defaultValue, string source, string propName, List<ConfigurationNotice> warnings)
    {
        if (value is null) return defaultValue;

        if (!TryParseByteSize(value, out var result))
        {
            warnings.Add(new ConfigurationNotice($"Invalid value for {propName}: {value}. Using default value.", source));
            return defaultValue;
        }

        return result;
    }

    private static string ResolveSocketPath(string? value, string defaultValue, string source, List<ConfigurationNotice> warnings)
    {
        if (value is not null) return value;

        // We only want to add this warning if:
        //  - We are not on Windows, it is standard that XDG_RUNTIME_DIR does not exist on Windows
        //  - XDG_RUNTIME_DIR is not set
        //  - The user hasn't specified another location for the socket
        if (!OperatingSystem.IsWindows() && HexusPaths.XdgRuntime is null)
        {
            warnings.Add(new ConfigurationNotice($"The XDG_RUNTIME_DIR environment is missing. Using default socket location ({defaultValue}).", source));
        }

        return defaultValue;
    }

    // Parsers

    private static bool TryParseTimeSpan(string value, out TimeSpan result)
    {
        var match = TimeSpanRegex().Match(value);

        // The first group is the entire match, so we skip it and check if any of the other groups matched.
        if (!match.Groups.Values.Skip(1).Any(g => g.Success))
        {
            result = TimeSpan.Zero;
            return false;
        }

        if (!long.TryParse(match.Groups[1].Value, out var hours))
        {
            hours = 0;
        }

        if (!long.TryParse(match.Groups[2].Value, out var minutes))
        {
            minutes = 0;
        }

        if (!double.TryParse(match.Groups[3].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds))
        {
            seconds = 0;
        }

        result = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        return true;
    }

    [GeneratedRegex(@"^\s*(?:(\d+)\s*h)?\s*(?:(\d+)\s*m)?\s*(?:(\d+(?:\.\d+)?)\s*s)?\s*$")]
    private static partial Regex TimeSpanRegex();

    private static bool TryParseByteSize(string value, out long result)
    {
        var match = ByteSizeRegex().Match(value);

        if (!match.Success || !long.TryParse(match.Groups[1].Value, out var size))
        {
            result = 0;
            return false;
        }

        var unit = match.Groups[2].Value.ToUpperInvariant();

        result = unit switch
        {
            "" => size,
            "K" => size * 1024,
            "M" => size * 1024 * 1024,
            "G" => size * 1024 * 1024 * 1024,
            _ => -1,
        };
        return result is not -1;
    }

    [GeneratedRegex(@"^\s*(\d+)\s*(|K|M|G)B\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ByteSizeRegex();
}
