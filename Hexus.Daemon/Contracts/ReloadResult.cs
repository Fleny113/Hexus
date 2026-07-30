using Hexus.Configuration;

namespace Hexus.Daemon.Contracts;

public sealed record ReloadResult(IEnumerable<string> Actions, IEnumerable<ConfigurationNotice> Warnings, IEnumerable<ConfigurationNotice> Errors);
