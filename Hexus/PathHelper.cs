namespace Hexus;

internal static class PathHelper
{
    public static string ResolveExecutable(string executable)
    {
        // relative folders resolver (./../exe)
        var absolutePath = Path.GetFullPath(executable);

        if (File.Exists(absolutePath))
            return absolutePath;

        // PATH env resolver
        if (executable.Contains(Path.DirectorySeparatorChar))
            throw new Exception("Executable cannot have slashes");

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? throw new Exception("Cannot get PATH environment variable");

        // Linux and Windows use different split char for the path
        var paths = pathEnv.Split(Path.PathSeparator);

        var resolvedExecutable = paths
            .SelectMany<string, string>(path =>
            {
                var file = Path.Combine(path, executable);

                // On windows we want to check for either .exe, .com, .bat or .cmd files
                if (OperatingSystem.IsWindows()) return [$"{file}.exe", $"{file}.com", $"{file}.bat", $"{file}.cmd"];

                return [file];
            })
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .FirstOrDefault();

        if (resolvedExecutable is not null)
            return resolvedExecutable;

        // No executable found
        throw new FileNotFoundException("Cannot find the executable");
    }
}
