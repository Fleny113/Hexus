using Hexus.Configuration;

namespace Hexus.Daemon.Services;

internal interface IConfigRelodable
{
    ReloadResult ReloadConfiguration(ConfigurationDiff diff);
}

public sealed record ConfigurationDiff
{
    public required IReadOnlyList<ApplicationConfiguration> Added { get; init; }
    public required IReadOnlyList<ApplicationConfiguration> Removed { get; init; }
    public required IReadOnlyList<(ApplicationConfiguration Old, ApplicationConfiguration New)> Modified { get; init; }
    public required DaemonConfiguration OldConfiguration { get; init; }
    public required DaemonConfiguration NewConfiguration { get; init; }
}
