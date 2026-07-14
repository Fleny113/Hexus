namespace Hexus.Configuration;

public sealed record ApplicationConfiguration
{
    public required string Name { get; init; }
    public required string Executable { get; init; }
    public string? Arguments { get; init; }
    public required string WorkingDirectory { get; init; }
    public required bool Enabled { get; init; }
    public string? Note { get; init; }
    public Dictionary<string, string> EnvironmentVariables { get; init; } = [];
    public long MemoryLimit { get; init; }

    internal sealed class ApplicationConfigurationRaw
    {
        public required string Executable { get; init; }
        public string? Arguments { get; init; }
        public required string WorkingDirectory { get; init; }
        public required bool Enabled { get; init; }
        public string? Note { get; init; }
        public Dictionary<string, string>? EnvironmentVariables { get; init; }
        public string? MemoryLimit { get; init; }
    }
}
