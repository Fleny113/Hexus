namespace Hexus.Daemon;

public sealed class HexusConfiguration
{
    public const string ConfigurationSection = "Hexus";

    public string Test { get; set; } = "Some random string";
    public required List<string> Array { get; set; }
    public required NestedHexusConfiguration Object { get; set; }
}

public sealed class NestedHexusConfiguration
{
    public required string Path { get; set; }
    public required string Executable { get; set; }
}
