using Hexus.Daemon.Extensions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hexus.Daemon.Configuration;

internal class HexusConfigurationManager
{
    public HexusConfiguration Configuration { get; private set; } = null!;

    private static readonly AppYamlSerializerContext _yamlStaticContext = new();
    private static readonly IDeserializer _yamlDeserializer = new StaticDeserializerBuilder(_yamlStaticContext)
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();
    private static readonly ISerializer _yamlSerializer = new StaticSerializerBuilder(_yamlStaticContext)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults | DefaultValuesHandling.OmitEmptyCollections)
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithIndentedSequences()
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

        lock (this)
        {
            var configurationFile = File.ReadAllText(EnvironmentHelper.ConfigurationFile);

            var configFile = _yamlDeserializer.Deserialize<HexusConfigurationFile?>(configurationFile) ?? new HexusConfigurationFile();

            Configuration = configFile.MapToConfig();
        }

        SaveConfiguration();
    }

    internal void SaveConfiguration()
    {
        EnvironmentHelper.EnsureDirectoriesExistence();

        var yamlString = _yamlSerializer.Serialize(Configuration.MapToConfigFile());

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
