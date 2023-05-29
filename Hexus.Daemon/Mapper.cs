using Hexus.Daemon.Endpoints;
using Riok.Mapperly.Abstractions;

namespace Hexus.Daemon;

[Mapper]
public static partial class Mapper
{
    [MapperIgnoreTarget(nameof(HexusApplication.Id))]
    public static partial HexusApplication RequestToApplication(this NewHexusApplicationRequest request);
}
