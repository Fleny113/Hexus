using System.ComponentModel;

namespace Hexus.Daemon.Configuration;

/// <summary>
/// Object used to consume the config file
/// </summary>
public sealed record HexusConfiguration
{
    public required string UnixSocket { get; init; }
    public int? HttpPort { get; init; }
    public double CpuRefreshIntervalSeconds { get; init; }
    public Dictionary<string, HexusApplication> Applications { get; init; } = [];
}

/// <summary>
/// Object used to write to the config file
/// </summary>
public sealed record HexusConfigurationFile
{
    public string? UnixSocket { get; init; }
    public int? HttpPort { get; init; }
    public double? CpuRefreshIntervalSeconds { get; init; }
    public IEnumerable<HexusApplication>? Applications { get; init; }
}
