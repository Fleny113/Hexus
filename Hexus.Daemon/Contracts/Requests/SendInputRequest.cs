using System.ComponentModel.DataAnnotations;

namespace Hexus.Daemon.Contracts.Requests;

public sealed record SendInputRequest(
    [property: Required] string Text,
    bool AddNewLine = true
) : IContract;
