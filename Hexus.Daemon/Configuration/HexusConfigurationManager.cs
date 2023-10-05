using Tomlyn;

namespace Hexus.Daemon.Configuration;


public class HexusConfigurationManager
{
    public HexusConfiguration Configuration { get; set; } = null!;

    public void LoadConfiguration()
    {
        EnvironmentHelper.EnsureDirectoriesExistence();

        if (!File.Exists(EnvironmentHelper.ConfigurationFile))
        {
            Configuration = new();
            return;
        }

        var configurationFile = File.ReadAllText(EnvironmentHelper.ConfigurationFile);

        Configuration = Toml.ToModel<HexusConfiguration>(configurationFile);
    }

    public void SaveConfiguration()
    {
        EnvironmentHelper.EnsureDirectoriesExistence();

        var config = Configuration;

        if (config.Applications.Count is 0)
            config = config with { Applications = null! };

        var tomlString = Toml.FromModel(config, new TomlModelOptions
        {
            ConvertToToml = input => {                   
                if (input is HexusApplicationStatus status)
                    return Enum.GetName(status);

                return input;
            }
        });

        lock (this)
        {
            File.WriteAllText(EnvironmentHelper.ConfigurationFile, tomlString);
        }
    }

    public HexusConfigurationManager()
    {
        LoadConfiguration();
    }

}
