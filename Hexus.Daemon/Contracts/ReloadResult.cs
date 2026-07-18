namespace Hexus.Daemon.Services;

public sealed record ReloadResult(IEnumerable<string> Actions, IEnumerable<string> Warnings, IEnumerable<string> Errors);
