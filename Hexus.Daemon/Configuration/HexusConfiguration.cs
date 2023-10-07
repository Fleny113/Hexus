using System.ComponentModel;

namespace Hexus.Daemon.Configuration;

public sealed record HexusConfiguration
{
    public string UnixSocket { get; set; } = EnvironmentHelper.SocketFile;

    [DefaultValue(-1)] public int HttpPort { get; set; } = -1;
    public Dictionary<string, HexusApplication> Applications { get; set; } = new();
}
