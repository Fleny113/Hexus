using System.Globalization;
using System.Text.RegularExpressions;
using Tomlyn;

namespace Hexus.Configuration;

public sealed partial class HexusConfigurationManager
{
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

    public IEnumerable<string> Warnings
    {
        get
        {
            using var _ = _lock.EnterScope();
            return field;
        }
        private set;
    }

    public IEnumerable<string> Errors
    {
        get
        {
            using var _ = _lock.EnterScope();
            return field;
        }
        private set;
    }

    public HexusConfigurationManager()
    {
        using var _ = _lock.EnterScope();

        EnvironmentHelper.EnsureDirectoriesExistence();

        var result = LoadDaemonConfiguration();
        DaemonConfiguration = result.Configuration;
        Warnings = result.Warnings;
        Errors = result.Errors;

        Applications = [];

        if (!Directory.Exists(EnvironmentHelper.ApplicationsConfigDirectory)) return;

        foreach (var file in Directory.EnumerateFiles(EnvironmentHelper.ApplicationsConfigDirectory, "*.toml"))
        {
            var appName = Path.GetFileNameWithoutExtension(file);
            var appResult = LoadApplicationConfiguration(appName);
            if (appResult.Configuration is not null)
            {
                Applications[appName] = appResult.Configuration;
            }
            Warnings = Warnings.Concat(appResult.Warnings);
            Errors = Errors.Concat(appResult.Errors);
        }
    }

    public static ConfigurationLoadResult<DaemonConfiguration> LoadDaemonConfiguration()
    {
        using var _ = _lock.EnterScope();

        if (!File.Exists(EnvironmentHelper.DaemonConfigFile))
        {
            return ResolveDaemonConfig(null);
        }

        using var file = File.OpenRead(EnvironmentHelper.DaemonConfigFile);
        if (!TomlSerializer.TryDeserialize<DaemonConfiguration.DaemonConfigurationRaw>(file, ConfigurationSerializerContext.Default, out var config))
        {
            return new(null!, [], ["Failed to parse daemon configuration file."]);
        }

        return ResolveDaemonConfig(config);
    }

    private ConfigurationLoadResult<ApplicationConfiguration?> LoadApplicationConfiguration(string applicationName)
    {
        using var _ = _lock.EnterScope();

        if (!File.Exists($"{EnvironmentHelper.ApplicationsConfigDirectory}/{applicationName}.toml"))
        {
            return new(null!, [], [$"Application configuration file for '{applicationName}' does not exist."]);
        }

        using var file = File.OpenRead($"{EnvironmentHelper.ApplicationsConfigDirectory}/{applicationName}.toml");

        if (!TomlSerializer.TryDeserialize<ApplicationConfiguration.ApplicationConfigurationRaw>(file, ConfigurationSerializerContext.Default, out var config))
        {
            return new(null!, [], [$"Failed to parse {applicationName} configuration file."]);
        }

        return ResolveApplicationConfig(config, applicationName);
    }

    private static ConfigurationLoadResult<DaemonConfiguration> ResolveDaemonConfig(DaemonConfiguration.DaemonConfigurationRaw? raw)
    {
        var defaultMemoryLimit = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 4;

        var warnings = new List<string>();
        var config = new DaemonConfiguration
        {
            UnixSocket = ResolveSocketPath(raw?.UnixSocket, EnvironmentHelper.DefaultSocketFile, warnings),
            HttpPort = raw?.HttpPort,
            CpuPollingInterval = ResolveTimeSpan(raw?.CpuPollingInterval, TimeSpan.FromSeconds(2.5), "cpu-polling-interval", warnings),
            MemoryPollingInterval = ResolveTimeSpan(raw?.MemoryPollingInterval, TimeSpan.FromSeconds(10), "memory-polling-interval", warnings),
            MemoryLimit = ResolveByteSize(raw?.MemoryLimit, defaultMemoryLimit, "memory-limit", warnings),
        };

        return new(config, warnings, []);
    }

    private ConfigurationLoadResult<ApplicationConfiguration?> ResolveApplicationConfig(ApplicationConfiguration.ApplicationConfigurationRaw raw, string name)
    {
        var warnings = new List<string>();

        var config = new ApplicationConfiguration
        {
            Name = name,
            Executable = raw.Exe,
            Arguments = raw.Args,
            WorkingDirectory = raw.WorkingDir,
            Enabled = raw.Enabled,
            Note = raw.Note,
            EnvironmentVariables = raw.Environment ?? [],
            MemoryLimit = ResolveByteSize(raw.MemoryLimit, DaemonConfiguration.MemoryLimit, "memory-limit", warnings),
        };

        return new(config, warnings, []);
    }

    public sealed record ConfigurationLoadResult<T>(T Configuration, IEnumerable<string> Warnings, IEnumerable<string> Errors);

    // Resolvers

    private static TimeSpan ResolveTimeSpan(string? value, TimeSpan defaultValue, string propName, List<string> warnings)
    {
        if (value is null) return defaultValue;

        if (!TryParseTimeSpan(value, out var result))
        {
            warnings.Add($"Invalid value for {propName}: {value}. Using default value.");
            return defaultValue;
        }

        return result;
    }

    private static long ResolveByteSize(string? value, long defaultValue, string propName, List<string> warnings)
    {
        if (value is null) return defaultValue;

        if (!TryParseByteSize(value, out var result))
        {
            warnings.Add($"Invalid value for {propName}: {value}. Using default value.");
            return defaultValue;
        }

        return result;
    }

    private static string ResolveSocketPath(string? value, string defaultValue, List<string> warnings)
    {
        if (value is not null) return value;

        // We only want to add this warning if:
        //  - We are not on Windows, it is standard that XDG_RUNTIME_DIR does not exist on Windows
        //  - XDG_RUNTIME_DIR is not set
        //  - The user hasn't specified another location for the socket
        if (!OperatingSystem.IsWindows() && EnvironmentHelper.XdgRuntime is null)
        {
            warnings.Add($"The XDG_RUNTIME_DIR environment is missing. Using default socket location ({defaultValue}).");
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
