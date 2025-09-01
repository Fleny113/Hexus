using Hexus.Daemon.Configuration;

namespace Hexus.Daemon.Contracts.Responses;

public sealed record ApplicationResponse(
    string Name,
    string Executable,
    string? Arguments,
    string WorkingDirectory,
    string? Note,
    Dictionary<string, string> EnvironmentVariables,
    HexusApplicationStatus Status,
    TimeSpan ProcessUptime,
    long ProcessId,
    double CpuUsage,
    long MemoryUsage,
    long? MemoryLimit
);
