using Hexus.Daemon.Interop;

namespace Hexus.Daemon.Configuration;

public static class EnvironmentHelper
{
    private static readonly bool IsDevelopment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";
    public static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // XDG directories based on the XDG basedir spec, we use these folders on Windows too.
    //
    // XDG_RUNTIME_DIR does not have a default we can point to due to the requirement this folder has (being owned by the user and being the only with Read Write Execute so 0o700)
    // This mean that we need to default to a directory in the temp, on Windows we instead use the XDG_STATE_HOME
    // as using the TEMP in Windows is unreliable as the socket file does not get locked so it is easly deleatable
    private static readonly string XdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? $"{Home}/.config";
    private static readonly string XdgState = Environment.GetEnvironmentVariable("XDG_STATE_HOME") ?? $"{Home}/.local/state";
    internal static readonly string? XdgRuntime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

    private static readonly string HexusStateDirectory = $"{XdgState}/hexus";
    private static readonly string HexusRuntimeDirectory = XdgRuntime ?? CreateRuntimeDirectory();

    public static readonly string LogFile = Path.GetFullPath(IsDevelopment ? $"{HexusStateDirectory}/daemon.dev.log" : $"{HexusStateDirectory}/daemon.log");
    public static readonly string ApplicationLogsDirectory = Path.GetFullPath($"{HexusStateDirectory}/applications");

    public static readonly string ConfigurationFile = Path.GetFullPath(IsDevelopment ? $"{XdgConfig}/hexus.dev.yaml" : $"{XdgConfig}/hexus.yaml");
    public static readonly string SocketFile = Path.GetFullPath(IsDevelopment ? $"{HexusRuntimeDirectory}/hexus.dev.sock" : $"{HexusRuntimeDirectory}/hexus.sock");

    public static void EnsureDirectoriesExistence()
    {
        // We don't want to create the runtime directory if it doesn't exist
        // The check is performed on the env itself to prevent erroring if we are falling back to something else (the XDG_STATE_HOME on Windows and /tmp/hexus-runtime on Linux)
        if (XdgRuntime is not null && !Directory.Exists(XdgRuntime))
        {
            throw new InvalidOperationException("The directory $XDG_RUNTIME_DIR does not exist.");
        }

        Directory.CreateDirectory(XdgConfig);
        Directory.CreateDirectory(HexusStateDirectory);
        Directory.CreateDirectory(HexusRuntimeDirectory);
        Directory.CreateDirectory(ApplicationLogsDirectory);
    }

    private static string CreateRuntimeDirectory()
    {
        // For Windows, we just put the runtime files in the XDG_STATE_HOME directory as we don't have many other solutions available
        if (OperatingSystem.IsWindows())
        {
            return HexusStateDirectory;
        }

        var uid = UnixInterop.GetUserId();
        var dir = Directory.CreateDirectory($"{Path.GetTempPath()}/{uid}-runtime", UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return dir.FullName;
    }
}
