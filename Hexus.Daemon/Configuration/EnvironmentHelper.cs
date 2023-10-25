namespace Hexus.Daemon.Configuration;

public static class EnvironmentHelper
{
    public static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static readonly string XdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? $"{Home}/.config";
    private static readonly string XdgState = Environment.GetEnvironmentVariable("XDG_STATE_HOME") ?? $"{Home}/.local/state";

    private static readonly string HexusStateDirectory = $"{XdgState}/hexus";

    public static readonly string LogsDirectory = $"{HexusStateDirectory}/logs";

    public static readonly string ConfigurationFile = $"{XdgConfig}/hexus.yaml";
    public static readonly string DevelopmentConfigurationFile = $"{XdgConfig}/hexus.dev.yaml";
    public static readonly string SocketFile = $"{HexusStateDirectory}/daemon.sock";

    public static void EnsureDirectoriesExistence()
    {
        Directory.CreateDirectory(HexusStateDirectory);
        Directory.CreateDirectory(XdgConfig);

        Directory.CreateDirectory(LogsDirectory);
    }
}
