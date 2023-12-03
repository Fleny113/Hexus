using Hexus.Daemon.Configuration;

namespace Hexus.Daemon.Contracts;

public sealed record HexusApplicationResponse(
    string Name,
    string Executable,
    string Arguments,
    string WorkingDirectory,
    Dictionary<string, string> EnvironmentVariables,
    HexusApplicationStatus Status,
    TimeSpan ProcessUptime,
    long ProcessId,
    double CpuUsage,
    long MemoryUsage
);
