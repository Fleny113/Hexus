using Hexus.Daemon.Configuration;

namespace Hexus;
internal static class PathHelper
{
    public static string ResolveExecutable(string executable)
    {
        // relative folders resolver (./.../exe)
        var absolutePath = EnvironmentHelper.NormalizePath(executable);

        if (File.Exists(absolutePath))
            return absolutePath;

        // PATH env resolver
        if (executable.Contains('/') || executable.Contains('\\'))
            throw new Exception("Executable cannot have slashes");

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? throw new Exception("Cannot get PATH environment variable");

        // Linux and Windows use different split char for the path
        var paths = pathEnv.Split(OperatingSystem.IsWindows() ? ';' : ':');

        var resolvedExecutable = paths
            .SelectMany<string, string>(path =>
            {
                var file = Path.Combine(path, executable);

                // On windows we want to check for either .exe or .com files
                if (OperatingSystem.IsWindows()) return [$"{file}.exe", $"{file}.com"];

                return [file];
            })
            .Where(File.Exists)
            .Select(EnvironmentHelper.NormalizePath)
            .FirstOrDefault();

        if (resolvedExecutable is not null)
            return resolvedExecutable;

        // No executable found
        throw new FileNotFoundException("Cannot find the executable");
    }
}
