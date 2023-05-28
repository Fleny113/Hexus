using Hexus.Daemon.Endpoints;
using Riok.Mapperly.Abstractions;

namespace Hexus.Daemon;

[Mapper]
public static partial class Mapper
{
    public static partial HexusApplication RequestToApplication(this NewHexusApplicationRequest request);
}
