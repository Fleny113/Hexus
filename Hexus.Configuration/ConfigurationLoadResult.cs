namespace Hexus.Configuration;

public sealed record ConfigurationLoadResult<T>(T Configuration, IEnumerable<ConfigurationNotice> Warnings, IEnumerable<ConfigurationNotice> Errors);
