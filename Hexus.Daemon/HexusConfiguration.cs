using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hexus.Daemon;

public sealed class HexusConfiguration
{
    public static readonly string ConfigurationFilePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.config/hexus.yml";
    public const string ConfigurationSection = "Hexus";

    public string Test { get; set; } = "Some random string";
    public required List<string> Array { get; set; }
    public required NestedHexusConfiguration Object { get; set; }

    private static readonly ISerializer _yamlSerializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    internal void SaveConfigurationToDisk()
    {
        var yaml = _yamlSerializer.Serialize(this);

        var directoryPath = Path.GetDirectoryName(ConfigurationFilePath) ?? throw new Exception("Can't get directory of the save file");

        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(ConfigurationFilePath, yaml);
    }
}

public sealed class NestedHexusConfiguration
{
    public required string Path { get; set; }
    public required string Executable { get; set; }
}
