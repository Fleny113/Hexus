using System.Runtime.InteropServices;

namespace Hexus.Configuration;

public static partial class EnvironmentHelper
{
    private static readonly bool IsDevelopment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";
    public static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // XDG directories based on the XDG basedir spec, we use these folders on Windows too.
    //
    // XDG_RUNTIME_DIR does not have a default we can point to due to the requirement this folder has (being owned by the user and being the only with Read Write Execute so 0o700)
    // This mean that we need to default to a directory in the temp, on Windows we instead use the XDG_STATE_HOME
    // as using the TEMP in Windows is unreliable as the socket file does not get locked so it is easily deletable
    public static readonly string XdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(Home, ".config");
    private static readonly string XdgState = Environment.GetEnvironmentVariable("XDG_STATE_HOME") ?? Path.Combine(Home, ".local", "state");
    internal static readonly string? XdgRuntime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

    public static readonly string FileSuffix = IsDevelopment ? ".dev" : "";

    private static readonly string HexusConfigDirectory = Path.Combine(XdgConfig, $"hexus{FileSuffix}");
    private static readonly string HexusStateDirectory = Path.Combine(XdgState, $"hexus{FileSuffix}");
    private static readonly string HexusRuntimeDirectory = XdgRuntime is not null ? Path.Combine(XdgRuntime, $"hexus{FileSuffix}") : CreateRuntimeDirectory();

    public static readonly string DaemonConfigFile = Path.Combine(HexusConfigDirectory, "daemon.toml");
    public static readonly string ApplicationsConfigDirectory = Path.Combine(HexusConfigDirectory, "applications");
    public static readonly string LogFile = Path.Combine(HexusStateDirectory, "daemon.log");
    public static readonly string ApplicationLogsDirectory = Path.Combine(HexusStateDirectory, "logs");
    public static readonly string ApplicationStatesDirectory = Path.Combine(HexusStateDirectory, "states");
    public static readonly string DefaultSocketFile = Path.Combine(HexusRuntimeDirectory, "daemon.sock");

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
        Directory.CreateDirectory(HexusConfigDirectory);
        Directory.CreateDirectory(ApplicationsConfigDirectory);
        Directory.CreateDirectory(ApplicationLogsDirectory);
        Directory.CreateDirectory(ApplicationStatesDirectory);
    }

    private static string CreateRuntimeDirectory()
    {
        // For Windows, we just put the runtime files in the XDG_STATE_HOME directory as we don't have many other solutions available
        if (OperatingSystem.IsWindows())
        {
            return HexusStateDirectory;
        }

        var uid = GetUserId();
        var dir = Directory.CreateDirectory($"{Path.GetTempPath()}/heuxs-{uid}{FileSuffix}", UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return dir.FullName;
    }

    [LibraryImport("libc", EntryPoint = "getuid", SetLastError = true)]
    private static partial int GetUserId();
}
