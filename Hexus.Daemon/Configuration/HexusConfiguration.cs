namespace Hexus.Daemon.Configuration;

public sealed record HexusConfiguration : HexusConfigurationFile
{
    public new Dictionary<string, HexusApplication> Applications { get; init; } = [];
}
