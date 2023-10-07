using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hexus.Daemon.Configuration;

public class HexusConfigurationManager
{
    public HexusConfiguration Configuration { get; set; } = null!;
    public static string ConfigurationFile { get; set; } = EnvironmentHelper.ConfigurationFile;

    private static readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly ISerializer _yamlSerializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
        .WithIndentedSequences()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public void LoadConfiguration()
    {
        EnvironmentHelper.EnsureDirectoriesExistence();

        if (!File.Exists(ConfigurationFile))
        {
            Configuration = new();
            return;
        }

        var configurationFile = File.ReadAllText(ConfigurationFile);

        Configuration = _yamlDeserializer.Deserialize<HexusConfiguration>(configurationFile);
    }

    public void SaveConfiguration()
    {
        EnvironmentHelper.EnsureDirectoriesExistence();

        var yamlString = _yamlSerializer.Serialize(Configuration);

        lock (this)
        {
            File.WriteAllText(ConfigurationFile, yamlString);
        }
    }

    public HexusConfigurationManager(bool isDevelopment)
    {
        // If we are in development mode we can change to another file to not pollute the normal file
        if (isDevelopment)
            ConfigurationFile = EnvironmentHelper.DevelopmentConfigurationFile;

        LoadConfiguration();
    }

}
