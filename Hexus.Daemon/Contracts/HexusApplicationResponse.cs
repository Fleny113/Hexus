using Hexus.Daemon.Configuration;

namespace Hexus.Daemon.Contracts;

public sealed record HexusApplicationResponse(
    string Name,
    string Executable,
    string Arguments,
    string WorkingDirectory,
    HexusApplicationStatus Status,
    double CpuUsage,
    long MemoryUsage
);
