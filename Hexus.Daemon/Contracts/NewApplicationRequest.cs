using Hexus.Daemon.Validators;
using System.ComponentModel.DataAnnotations;

namespace Hexus.Daemon.Contracts;

public sealed record NewApplicationRequest(
    [property: Required] string Name,
    [property: Required, AbsolutePath] string Executable,
    string Arguments = "",
    string WorkingDirectory = "",
    string Note = "",
    Dictionary<string, string>? EnvironmentVariables = null
) : IContract
{
    [Required, AbsolutePath] public string WorkingDirectory { get; set; } = WorkingDirectory;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = EnvironmentVariables ?? [];
}
