using System.ComponentModel;

namespace Hexus.Daemon.Configuration;

public sealed record HexusConfiguration
{
    public string UnixSocket { get; init; } = EnvironmentHelper.SocketFile;
    public int? HttpPort { get; init; }
    public double CpuRefreshIntervalSeconds { get; init; }
    public Dictionary<string, HexusApplication> Applications { get; init; } = [];
}


public sealed record HexusConfigurationFile
{
    public string UnixSocket { get; init; } = EnvironmentHelper.SocketFile;
    public int? HttpPort { get; init; }
    [DefaultValue(1d)] public double CpuRefreshIntervalSeconds { get; init; } = 1d;
    public IEnumerable<HexusApplication>? Applications { get; init; }
}
