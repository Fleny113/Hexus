using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hexus.Daemon.Configuration;

internal class HexusConfigurationManager
{
    public HexusConfiguration Configuration { get; private set; } = null!;
    private readonly string _configurationFile = EnvironmentHelper.ConfigurationFile;
    private readonly string _socketFile = EnvironmentHelper.SocketFile;

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .WithIndentedSequences()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private void LoadConfiguration()
    {
        EnvironmentHelper.EnsureDirectoriesExistence();

        if (!File.Exists(_configurationFile))
        {
            Configuration = new HexusConfiguration { UnixSocket = _socketFile };

            SaveConfiguration();
            return;
        }

        var configurationFile = File.ReadAllText(_configurationFile);

        // For whatever reason: if the yaml deserializer receives an empty string, it uses null for the result
        var configFile = YamlDeserializer.Deserialize<HexusConfigurationFile?>(configurationFile) ?? new HexusConfigurationFile();

        Configuration = new HexusConfiguration
        {
            UnixSocket = configFile.UnixSocket ?? _socketFile,
            HttpPort = configFile.HttpPort,
            CpuRefreshIntervalSeconds = configFile.CpuRefreshIntervalSeconds,
            Applications = configFile.Applications?.ToDictionary(application => application.Name) ?? [],
        };

        SaveConfiguration();
    }

    internal void SaveConfiguration()
    {
        EnvironmentHelper.EnsureDirectoriesExistence();

        var configFile = new HexusConfigurationFile
        {
            UnixSocket = Configuration.UnixSocket,
            HttpPort = Configuration.HttpPort,
            CpuRefreshIntervalSeconds = Configuration.CpuRefreshIntervalSeconds,
            Applications = Configuration.Applications.Values,
        };

        var yamlString = YamlSerializer.Serialize(configFile);

        lock (this)
        {
            File.WriteAllText(_configurationFile, yamlString);
        }
    }

    internal HexusConfigurationManager(bool isDevelopment)
    {
        // If we are in development mode we can change to another file to not pollute the normal file
        if (isDevelopment)
        {
            _configurationFile = EnvironmentHelper.DevelopmentConfigurationFile;
            _socketFile = EnvironmentHelper.DevelopmentSocketFile;
        }

        EnvironmentHelper.EnsureDirectoriesExistence();

        try
        {
            LoadConfiguration();
        }
        catch (Exception exception)
        {
            throw new Exception("Unable to parse the configuration file", exception);
        }
    }
}
