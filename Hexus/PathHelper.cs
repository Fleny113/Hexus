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
        return ResolveExecutableInPath(executable) ?? throw new FileNotFoundException($"Cannot find the executable '{executable}'");
    }

    public static string? ResolveExecutableInPath(string executable)
    {
        if (executable.Contains(Path.DirectorySeparatorChar))
            throw new Exception("Executable name cannot have slashes");

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? throw new Exception("Cannot get PATH environment variable");

        // Linux and Windows use different split char for the path
        var paths = pathEnv.Split(Path.PathSeparator);

        return paths
            .SelectMany<string, string>(path =>
            {
                var file = Path.Combine(path, executable);

                // On windows we want to check for either .exe, .com, .bat or .cmd files
                // We also check for the file itself in case the user already specified the extension or a extension-less file exists in the path
                if (OperatingSystem.IsWindows()) return [file, $"{file}.exe", $"{file}.com", $"{file}.bat", $"{file}.cmd"];

                return [file];
            })
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .FirstOrDefault();
    }
}
