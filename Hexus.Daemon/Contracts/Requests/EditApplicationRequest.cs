namespace Hexus.Daemon.Contracts.Requests;

public sealed record EditApplicationRequest(
    string? Name = null,
    string? Executable = null,
    string? Arguments = null,
    string? WorkingDirectory = null,
    string? Note = null,
    Dictionary<string, string>? NewEnvironmentVariables = null,
    string[]? RemoveEnvironmentVariables = null,
    bool? IsReloadingEnvironmentVariables = null,
    long? MemoryLimit = null
);
