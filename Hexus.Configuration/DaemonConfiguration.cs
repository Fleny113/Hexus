namespace Hexus.Configuration;

public sealed record DaemonConfiguration
{
    public required string UnixSocket { get; init; }
    public int? HttpPort { get; init; }
    public required TimeSpan CpuPollingInterval { get; init; }
    public required TimeSpan MemoryPollingInterval { get; init; }
    public required long MemoryLimit { get; init; }

    internal sealed class DaemonConfigurationRaw
    {
        public string? UnixSocket { get; init; }
        public int? HttpPort { get; init; }
        public string? CpuPollingInterval { get; init; }
        public string? MemoryPollingInterval { get; init; }
        public string? MemoryLimit { get; init; }
    }
}
