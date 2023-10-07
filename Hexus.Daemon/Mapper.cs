using Hexus.Daemon.Endpoints;
using Riok.Mapperly.Abstractions;

namespace Hexus.Daemon;

[Mapper]
public static partial class Mapper
{
    [MapperIgnoreTarget(nameof(HexusApplication.Status)), MapperIgnoreTarget(nameof(HexusApplication.Process))]
    public static partial HexusApplication MapToApplication(this NewApplicationRequest request);
}
