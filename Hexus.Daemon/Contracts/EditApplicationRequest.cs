using Hexus.Daemon.Validators;

namespace Hexus.Daemon.Contracts;

public record EditApplicationRequest(
    string? Name, 
    [property: AbsolutePath] string? Executable, 
    string? Arguments, 
    [property: AbsolutePath] string? WorkingDirectory
) : IContract;
