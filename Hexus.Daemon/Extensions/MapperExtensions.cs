using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;

namespace Hexus.Daemon.Extensions;

internal static class MapperExtensions
{
    public static HexusApplication MapToApplication(this NewApplicationRequest request)
    {
        return new HexusApplication
        {
            Name = request.Name,
            Executable = request.Executable,
            Arguments = request.Arguments,
            WorkingDirectory = request.WorkingDirectory,
        };
    }

    public static HexusApplicationResponse MapToResponse(this HexusApplication application)
    {
        return new HexusApplicationResponse();
    }
}
