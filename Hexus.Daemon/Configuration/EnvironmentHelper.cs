namespace Hexus.Daemon.Configuration;

public static class EnvironmentHelper
{
    public static readonly string Home = NormalizePath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    private static readonly string XdgConfig = NormalizePath(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? $"{Home}/.config");
    private static readonly string XdgState = NormalizePath(Environment.GetEnvironmentVariable("XDG_STATE_HOME") ?? $"{Home}/.local/state");

    private static readonly string HexusStateDirectory = NormalizePath($"{XdgState}/hexus");

    public static readonly string LogsDirectory = NormalizePath($"{HexusStateDirectory}/logs");

    public static readonly string ConfigurationFile = NormalizePath($"{XdgConfig}/hexus.yaml");
    public static readonly string DevelopmentConfigurationFile = NormalizePath($"{XdgConfig}/hexus.dev.yaml");
    public static readonly string SocketFile = NormalizePath($"{HexusStateDirectory}/daemon.sock");

    // Used by the CLI to detect when to use the Development configuration
    public static readonly bool IsDevelopment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";

    public static void EnsureDirectoriesExistence()
    {
        Directory.CreateDirectory(HexusStateDirectory);
        Directory.CreateDirectory(XdgConfig);

        Directory.CreateDirectory(LogsDirectory);
    }
    
    public static string NormalizePath(string path)
    {
        return Path.GetFullPath(path);
    }
}
