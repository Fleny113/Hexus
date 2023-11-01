using Hexus.Daemon.Validators;
using System.ComponentModel.DataAnnotations;

namespace Hexus.Daemon.Contracts;

public sealed record NewApplicationRequest(
    [property: Required] string Name, 
    [property: AbsolutePath] string Executable, 
    string Arguments = "", 
    string WorkingDirectory = ""
) : IContract
{
    [AbsolutePath] public string WorkingDirectory { get; set; } = WorkingDirectory;
}
