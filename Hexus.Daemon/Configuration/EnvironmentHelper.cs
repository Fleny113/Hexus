namespace Hexus.Daemon.Configuration;

public static class EnvironmentHelper
{
    public static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static readonly string XDGConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? $"{Home}/.config";
    public static readonly string XDGState = Environment.GetEnvironmentVariable("XDG_STATE_HOME") ?? $"{Home}/.local/state";

    public static readonly string HexusStateDirectory = $"{XDGState}/hexus";

    public static readonly string LogsDirectory = $"{HexusStateDirectory}/logs";

    public static readonly string ConfigurationFile = $"{XDGConfig}/hexus.yaml";
    public static readonly string DevelopmentConfigurationFile = $"{XDGConfig}/hexus.dev.yaml";
    public static readonly string SocketFile = $"{HexusStateDirectory}/daemon.sock";

    public static void EnsureDirectoriesExistence()
    {
        Directory.CreateDirectory(HexusStateDirectory);
        Directory.CreateDirectory(XDGConfig);

        Directory.CreateDirectory(LogsDirectory);
    }
}
