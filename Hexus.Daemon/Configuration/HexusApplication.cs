using System.ComponentModel;

namespace Hexus.Daemon.Configuration;

public sealed record HexusApplication
{
    public required string Name { get; set; }
    public required string Executable { get; set; }

    [DefaultValue("")] public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public HexusApplicationStatus Status { get; set; } = HexusApplicationStatus.Exited;
    [DefaultValue("")] public string Note { get; set; } = "";

    public Dictionary<string, string> EnvironmentVariables { get; set; } = [];
}
