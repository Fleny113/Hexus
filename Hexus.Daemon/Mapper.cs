using Hexus.Daemon.Endpoints;

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
