using System.Collections.Immutable;

namespace Hexus.Configuration;

public sealed record ApplicationConfiguration
{
    public required string Name { get; init; }
    public required string Executable { get; init; }
    public string? Arguments { get; init; }
    public required string WorkingDirectory { get; init; }
    public required bool Enabled { get; init; }
    public string? Note { get; init; }
    public required ImmutableDictionary<string, string> EnvironmentVariables { get; init; }
    public long MemoryLimit { get; init; }

    public sealed class ApplicationConfigurationRaw
    {
        public required string Exe { get; init; }
        public string? Args { get; init; }
        public required string WorkingDir { get; init; }
        public required bool Enabled { get; init; }
        public string? Note { get; init; }
        public Dictionary<string, string>? Environment { get; init; }
        public string? MemoryLimit { get; init; }
    }
}
