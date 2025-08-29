using YamlDotNet.Serialization;

namespace Hexus.Daemon.Configuration;

public sealed record HexusApplication
{
    [YamlIgnore]
    public string Name { get; set; } = null!;
    public string Executable { get; set; } = null!;

    public string? Arguments { get; set; }
    public string WorkingDirectory { get; set; } = null!;
    public HexusApplicationStatus Status { get; set; } = HexusApplicationStatus.Exited;
    public string? Note { get; set; }

    public Dictionary<string, string> EnvironmentVariables { get; set; } = [];

    public uint? MemoryLimit { get; set; }
}
