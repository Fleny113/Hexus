using System.Collections.Immutable;

namespace Hexus.Daemon.Contracts.Responses;

public sealed record ApplicationResponse(
    string Name,
    string Executable,
    string? Arguments,
    string WorkingDirectory,
    bool Enabled,
    string? Note,
    ImmutableDictionary<string, string> EnvironmentVariables,
    ApplicationStatus Status,
    TimeSpan ProcessUptime,
    long ProcessId,
    double CpuUsage,
    long MemoryUsage,
    long MemoryLimit
);
