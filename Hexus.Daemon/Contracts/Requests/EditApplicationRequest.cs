using Hexus.Daemon.Validators;

namespace Hexus.Daemon.Contracts.Requests;

public record EditApplicationRequest(
    string? Name = null,
    [property: AbsolutePath] string? Executable = null,
    string? Arguments = null,
    [property: AbsolutePath] string? WorkingDirectory = null,
    string? Note = null,
    Dictionary<string, string>? NewEnvironmentVariables = null,
    string[]? RemoveEnvironmentVariables = null,
    bool? IsReloadingEnvironmentVariables = null
) : IContract;
