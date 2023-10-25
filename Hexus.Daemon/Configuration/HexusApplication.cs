using System.ComponentModel;
using System.Diagnostics;
using YamlDotNet.Serialization;

namespace Hexus.Daemon.Configuration;

public sealed record HexusApplication
{
    public required string Name { get; set; }
    public required string Executable { get; set; }

    [DefaultValue("")] public string Arguments { get; set; } = "";
    [DefaultValue("")] public string WorkingDirectory { get; set; } = "";
    public HexusApplicationStatus Status { get; set; } = HexusApplicationStatus.Exited;

    // Internal proprieties
    [YamlIgnore] internal Process? Process { get; set; }
    [YamlIgnore] internal StreamWriter? LogFile { get; set; }
}
