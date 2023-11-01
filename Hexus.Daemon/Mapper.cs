using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;

namespace Hexus.Daemon;

internal static class Mapper
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
}
