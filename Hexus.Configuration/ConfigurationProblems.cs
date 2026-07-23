namespace Hexus.Configuration;

public sealed record ConfigurationProblems(IEnumerable<string> Warnings, IEnumerable<string> Errors);
