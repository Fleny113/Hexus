namespace Hexus.Daemon.Configuration;

public sealed record HexusConfiguration
{
    public required string UnixSocket { get; init; }
    public int? HttpPort { get; init; }
    public double CpuRefreshIntervalSeconds { get; init; }
    public Dictionary<string, HexusApplication> Applications { get; init; } = [];
}

// Used for the YAML File serialization
internal sealed record HexusConfigurationFile
{
    public string? UnixSocket { get; set; }
    public int? HttpPort { get; set; }
    public double? CpuRefreshIntervalSeconds { get; set; }
    public Dictionary<string, HexusApplication>? Applications { get; set; }
}
