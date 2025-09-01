namespace Hexus.Daemon.Contracts.Requests;

public sealed record NewApplicationRequest(
    string Name,
    string Executable,
    string Arguments = "",
    string? WorkingDirectory = null,
    string Note = "",
    Dictionary<string, string>? EnvironmentVariables = null,
    long? MemoryLimit = null
);
