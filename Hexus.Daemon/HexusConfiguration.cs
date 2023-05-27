using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hexus.Daemon;

public sealed record HexusConfiguration
{

    private static readonly ISerializer _yamlSerializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static readonly string HexusHomeFolder = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}{Path.DirectorySeparatorChar}.hexus";
    public static readonly string ConfigurationFilePath = $"{HexusHomeFolder}{Path.DirectorySeparatorChar}config.yml";
    public const string ConfigurationSection = "Hexus";

    public string? UnixSocket { get; set; } = $"{HexusHomeFolder}{Path.DirectorySeparatorChar}hexus.sock";
    public int HttpPort { get; set; } = -1;
    public bool Localhost { get; set; } = true;

    public List<HexusApplication> Applications { get; set; } = Enumerable.Empty<HexusApplication>().ToList();

    internal void SaveConfigurationToDisk()
    {
        var yaml = _yamlSerializer.Serialize(this);

        var directoryPath = Path.GetDirectoryName(ConfigurationFilePath) ?? throw new Exception("Can't get directory of the save file");

        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(ConfigurationFilePath, yaml);
    }
}
