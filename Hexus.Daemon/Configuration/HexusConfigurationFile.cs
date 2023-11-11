using System.ComponentModel;

namespace Hexus.Daemon.Configuration;

public record HexusConfigurationFile
{
    public string UnixSocket { get; init; } = EnvironmentHelper.SocketFile;
    [DefaultValue(-1)] public int HttpPort { get; init; } = -1;
    public IEnumerable<HexusApplication> Applications { get; init; } = Enumerable.Empty<HexusApplication>();
}
