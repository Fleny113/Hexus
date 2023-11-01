using System.ComponentModel.DataAnnotations;

namespace Hexus.Daemon.Contracts;

public sealed record SendInputRequest(
    [property: Required] string Text, 
    bool AddNewLine = true
) : IContract;
