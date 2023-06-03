using Hexus.Daemon.Endpoints;
using Riok.Mapperly.Abstractions;

namespace Hexus.Daemon;

[Mapper]
public static partial class Mapper
{
    [MapperIgnoreTarget(nameof(HexusApplication.Id)), MapperIgnoreTarget(nameof(HexusApplication.Status))]
    public static partial HexusApplication MapToApplication(this NewApplicationRequest request);
}
