using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hexus.Daemon.Configuration;

internal class HexusConfigurationManager
{
    public HexusConfiguration Configuration { get; private set; } = null!;
    public Dictionary<string, object?>? AppSettings { get; private set; }
    private readonly string _configurationFile = EnvironmentHelper.ConfigurationFile;
    private readonly string _socketFile = EnvironmentHelper.SocketFile;

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults | DefaultValuesHandling.OmitEmptyCollections)
        .WithIndentedSequences()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private void LoadConfiguration()
    {
        EnvironmentHelper.EnsureDirectoriesExistence();

        if (!File.Exists(_configurationFile))
        {
            Configuration = new HexusConfiguration
            {
                UnixSocket = _socketFile,
            };

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
        AppSettings = configFile.AppSettings;

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
            AppSettings = AppSettings,
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
    
    internal static IEnumerable<KeyValuePair<string, string?>> FlatDictionary(Dictionary<string, object?>? dictionary)
    {
        if (dictionary is null)
            return [];

        return dictionary
            .SelectMany(
                pair => pair.Value switch
                {
                    Dictionary<string, object?> subDictionary => FlatDictionary(subDictionary).AsEnumerable(),
                    _ => new KeyValuePair<string, string?>[] { new(pair.Key, pair.Value?.ToString()) },
                }
            );
    }
}
