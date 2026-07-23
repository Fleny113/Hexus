namespace Hexus.Daemon.Configuration;

// Used for the YAML File serialization
[Obsolete]
internal sealed record HexusConfigurationFile
{
    public string? UnixSocket { get; set; }
    public int? HttpPort { get; set; }
    public double? CpuRefreshIntervalSeconds { get; set; }
    public double? MemoryLimitCheckIntervalSeconds { get; set; }
    public long? MemoryLimit { get; set; }
    public Dictionary<string, HexusApplication>? Applications { get; set; }
}
