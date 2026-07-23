namespace Hexus.Daemon.Contracts;

public sealed record ReloadResult(IEnumerable<string> Actions, IEnumerable<string> Warnings, IEnumerable<string> Errors);
