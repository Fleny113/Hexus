namespace Hexus.Daemon.Contracts.Requests;

public sealed record SendInputRequest(string Text, bool AddNewLine = true);
