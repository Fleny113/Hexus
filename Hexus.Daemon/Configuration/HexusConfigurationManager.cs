using Hexus.Daemon.Extensions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hexus.Daemon.Configuration;

internal class HexusConfigurationManager
{
    public HexusConfiguration Configuration { get; private set; } = null!;

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
        .WithIndentedSequences()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private void LoadConfiguration()
    {
        EnvironmentHelper.EnsureDirectoriesExistence();

        if (!File.Exists(EnvironmentHelper.ConfigurationFile))
        {
            Configuration = new HexusConfiguration { UnixSocket = EnvironmentHelper.SocketFile };

            SaveConfiguration();
            return;
        }

        var configurationFile = File.ReadAllText(EnvironmentHelper.ConfigurationFile);
        var configFile = YamlDeserializer.Deserialize<HexusConfigurationFile?>(configurationFile) ?? new HexusConfigurationFile();

        Configuration = configFile.MapToConfig();

        SaveConfiguration();
    }

    internal void SaveConfiguration()
    {
        EnvironmentHelper.EnsureDirectoriesExistence();

        var yamlString = YamlSerializer.Serialize(Configuration.MapToConfigFile());

        lock (this)
        {
            File.WriteAllText(EnvironmentHelper.ConfigurationFile, yamlString);
        }
    }

    internal HexusConfigurationManager()
    {
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
