namespace Hexus.Configuration;

public sealed record ConfigurationProblems(IEnumerable<ConfigurationNotice> Warnings, IEnumerable<ConfigurationNotice> Errors);
