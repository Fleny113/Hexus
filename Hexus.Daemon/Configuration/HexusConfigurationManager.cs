using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hexus.Daemon.Configuration;

internal class HexusConfigurationManager
{
    public HexusConfiguration Configuration { get; private set; } = null!;
    private static string ConfigurationFile { get; set; } = EnvironmentHelper.ConfigurationFile;

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
        .WithIndentedSequences()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    internal void LoadConfiguration()
    {
        EnvironmentHelper.EnsureDirectoriesExistence();

        if (!File.Exists(ConfigurationFile))
        {
            Configuration = new HexusConfiguration();
            return;
        }

        var configurationFile = File.ReadAllText(ConfigurationFile);

        // For whatever reason: if the yaml deserializer receives an empty string, it uses null for the result
        Configuration = YamlDeserializer.Deserialize<HexusConfiguration?>(configurationFile) ?? new HexusConfiguration();
    }

    internal void SaveConfiguration()
    {
        EnvironmentHelper.EnsureDirectoriesExistence();

        var yamlString = YamlSerializer.Serialize(Configuration);

        lock (this)
        {
            File.WriteAllText(ConfigurationFile, yamlString);
        }
    }

    internal HexusConfigurationManager(bool isDevelopment)
    {
        // If we are in development mode we can change to another file to not pollute the normal file
        if (isDevelopment)
            ConfigurationFile = EnvironmentHelper.DevelopmentConfigurationFile;

        LoadConfiguration();
    }
}
