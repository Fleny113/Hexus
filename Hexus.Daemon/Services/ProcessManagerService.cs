using Hexus.Daemon.Configuration;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Hexus.Daemon.Services;

public partial class ProcessManagerService(ILogger<ProcessManagerService> logger, HexusConfigurationManager configManager)
{
    internal ConcurrentDictionary<Process, HexusApplication> Application { get; } = new();

    /// <summary> Start an instance of the application</summary>
    /// <param name="application">The application to start</param>
    /// <returns>Whatever if the application was started or not</returns>
    public bool StartApplication(HexusApplication application)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = application.Executable,
            Arguments = application.Arguments,
            WorkingDirectory = application.WorkingDirectory,

            CreateNoWindow = true,

            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,

            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,

            // NOTE: If set to UTF8 it may give issues when using the STDIN
            //  ASCII seems to solve the issue
            StandardInputEncoding = Encoding.ASCII,
        };

        var process = Process.Start(processInfo);

        if (process is null)
            return false;

        // Enable the emitting of events and the reading of the STDOUT and STDERR
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Register callbacks
        process.OutputDataReceived += HandleStdOutLogs;
        process.ErrorDataReceived += HandleStdErrLogs;

        process.Exited += AcknowledgeProcessExit;
        process.Exited += HandleProcessRestart;

        application.Process = process;

        application.LogFile?.Close();
        application.LogFile = File.AppendText($"{EnvironmentHelper.LogsDirectory}/{application.Name}.log");
        application.LogFile.AutoFlush = true;

        application.Status = HexusApplicationStatus.Operating;
        configManager.SaveConfiguration();

        Application.TryAdd(process, application);

        return true;
    }

    /// <summary>Stop the instance of an application</summary>
    /// <param name="name">The name of the application to stop</param>
    /// <returns>If the application was running</returns>
    public bool StopApplication(string name, bool forceStop = false)
    {
        if (!IsApplicationRunning(name, out var application) || application.Process is null)
            return false;

        // Remove the restart event handler, or else it will restart the process as soon as it stops
        application.Process.Exited -= HandleProcessRestart;

        KillProcessCore(application.Process, forceStop);

        application.Process.Close();
        application.Status = HexusApplicationStatus.Exited;

        configManager.SaveConfiguration();

        return true;
    }

    /// <summary>Given a name of an application check if it exists, is running and has an attached process running</summary>
    /// <param name="name">The name of the application</param>
    /// <param name="application">The application returned with the same <paramref name="name"/> string</param>
    /// <returns>If the application is running</returns>
    public bool IsApplicationRunning(string name, [NotNullWhen(true)] out HexusApplication? application)
    {
        if (!configManager.Configuration.Applications.TryGetValue(name, out application))
            return false;

        return IsApplicationRunning(application);
    }

    /// <summary>Check if an application exists, is running and has an attached process running</summary>
    /// <param name="application">The nullable instance of an <see cref="HexusApplication" /></param>
    /// <returns>If the application is running</returns>
    public bool IsApplicationRunning([NotNullWhen(true)] HexusApplication? application) 
        => application is { Status: HexusApplicationStatus.Operating, Process.HasExited: false };
 

    /// <summary>Send a message into the Standard Input (STDIN) of an application</summary>
    /// <param name="name">The name of the application</param>
    /// <param name="text">The text to send into the STDIN</param>
    /// <param name="newLine">Whatever or not to append an endline to the text</param>
    /// <returns>Whatever or not if the operation was successful or not</returns>
    public bool SendToApplication(string name, ReadOnlySpan<char> text, bool newLine = true)
    {
        if (!IsApplicationRunning(name, out var application) || application.Process is null)
            return false;

        if (newLine)
            application.Process.StandardInput.WriteLine(text);
        else
            application.Process.StandardInput.Write(text);

        return true;
    }

    private void KillProcessCore(Process process, bool forceStop)
    {
        if (forceStop)
        {
            process.Kill();
            return;
        }

        try
        {
            NativeKill(process.Id, WindowsSignal.SIGINT, UnixSignal.SIGINT);

            // If in 30 seconds the process doesn't get killed (it has handled the SIGINT signal and not exited) then force stop it
            if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
                process.Kill();

        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Error during the stop of a process");

            // If it has already exited there is no point in sending another kill
            if (process.HasExited)
                return;

            // Fallback to the .NET build-in Kernel call to force stop the process
            process.Kill();
        }
    }

    #region Log process events handlers

    private void ProcessApplicationLog(HexusApplication application, string logType, string message)
    {
        if (application is not { LogFile: StreamWriter, Process: Process })
            return;

	// Trying to get the PID of a exited process throws an error
        if (application.Process is { HasExited: false } && logger.IsEnabled(LogLevel.Trace))
            logger.LogTrace("{PID} says: '{OutputData}'", application.Process.Id, message);

        var date = DateTimeOffset.UtcNow.ToString("MMM dd yyyy HH:mm:ss");

        if (application.LogFile.BaseStream.CanWrite)
            application.LogFile.WriteLine($"[{date},{logType}] {message}");
    }

    private void HandleStdOutLogs(object? sender, DataReceivedEventArgs e)
    {
        if (sender is not Process process || !Application.TryGetValue(process, out var application))
            return;

        ProcessApplicationLog(application, "STDOUT", e.Data ?? "");
    }

    private void HandleStdErrLogs(object? sender, DataReceivedEventArgs e)
    {
        if (sender is not Process process || !Application.TryGetValue(process, out var application))
            return;

        ProcessApplicationLog(application, "STDERR", e.Data ?? "");
    }

    #endregion

    #region Exit process event handlers

    private const int _maxRestarts = 10;

    // If the application can't live for more then 30 seconds, after the 10 attempts to restart it, it will be considerate crashed
    private static readonly TimeSpan _resetTimeWindow = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, (int Restarts, CancellationTokenSource CTS)> _consequentialRestarts = new();

    private void AcknowledgeProcessExit(object? sender, EventArgs e)
    {
        if (sender is not Process process || !Application.TryGetValue(process, out var application))
            return;

        application.LogFile?.Flush();
        application.LogFile?.Close();

        application.Status = HexusApplicationStatus.Exited;
        configManager.SaveConfiguration();

        logger.LogDebug("Acknowledging about \"{Name}\" exiting with code: {ExitCode} [{PID}]", application.Name, process.ExitCode, process.Id);
    }

    private void HandleProcessRestart(object? sender, EventArgs e)
    {
        if (sender is not Process process || !Application.TryGetValue(process, out var application))
            return;

        // Fire and forget
        _ = HandleProcessRestartCoreAsync(application);
    }

    private async Task HandleProcessRestartCoreAsync(HexusApplication application)
    {
        if (_consequentialRestarts.TryGetValue(application.Name, out var tuple))
        {
            tuple.Restarts++;
            tuple.CTS.Dispose();
            tuple.CTS = new CancellationTokenSource(_resetTimeWindow);
        }
        else
            tuple = (1, new CancellationTokenSource(_resetTimeWindow));

        var (restarts, cts) = tuple;

        if (restarts >= _maxRestarts)
        {
            logger.LogWarning("Application \"{Name}\" has exited for {maxRestarts} times in the time window ({TimeWindow} seconds). It will be considered crashed", 
                application.Name, restarts, _resetTimeWindow.TotalSeconds);

            cts.Dispose();
            _consequentialRestarts.TryRemove(application.Name, out _);

            application.Status = HexusApplicationStatus.Crashed;
            configManager.SaveConfiguration();

            return;
        }

        var delay = CalculateDelay(restarts);
        cts.Token.Register(ResetConsequentialRestarts, application.Name);

        _consequentialRestarts[application.Name] = (restarts, cts);

        logger.LogDebug("Attempting to restart application \"{Name}\", waiting for {Seconds} seconds before restarting", application.Name, delay.TotalSeconds);

        await Task.Delay(delay);

        StartApplication(application);

        configManager.SaveConfiguration();
    }

    private void ResetConsequentialRestarts(object? state)
    {
        if (state is not string name)
            return;

        _consequentialRestarts.TryRemove(name, out var tuple);
        var (restarts, cts) = tuple;


        cts.Dispose();
        logger.LogDebug("After {Restarts} restarts, application \"{Name}\" stopped restarting", restarts, name);
    }

    private static TimeSpan CalculateDelay(int restart)
    {
        return restart switch
        {
            1 or 2 or 3 => TimeSpan.Zero,
            4 or 5 => TimeSpan.FromSeconds(1),
            6 or 7 => TimeSpan.FromSeconds(2),
            8 or 9 => TimeSpan.FromSeconds(4),
            10 => TimeSpan.FromSeconds(8),
            _ => throw new ArgumentOutOfRangeException(nameof(restart))
        };
    }

    #endregion

    #region Native Interop

    private enum UnixSignal : int
    {
        SIGHUP = 1,     // Hangup
        SIGINT = 2,     // Interrupt 
        SIGQUIT = 3,    // Quit
        SIGILL = 4,     // Illegal instruction 
        SIGTRAP = 5,    // Trace trap 
        SIGABRT = 6,    // Abort 
        SIGBUS = 7,     // BUS error
        SIGFPE = 8,     // Floating-point exception
        SIGKILL = 9,    // UnixKill, unblockable
        SIGUSR1 = 10,   // User-defined signal 1
        SIGSEGV = 11,   // Segmentation violation
        SIGUSR2 = 12,   // User-defined signal 2 
        SIGPIPE = 13,   // Broken pipe
        SIGALRM = 14,   // Alarm clock
        SIGTERM = 15,   // Termination 
        SIGCHLD = 16,   // Child status has changed
        SIGCONT = 17,   // Continue
        SIGSTOP = 18,   // Stop, unblockable
        SIGTSTP = 19,   // Keyboard stop
        SIGTTIN = 20,   // Background read from tty
        SIGTTOU = 21,   // Background write to tty
        SIGURG = 22,    // Urgent condition on socket
        SIGXCPU = 23,   // CPU limit exceeded
        SIGXFSZ = 24,   // File size limit exceeded
        SIGVTALRM = 25, // Virtual alarm clock
        SIGPROF = 26,   // Profiling alarm clock
        SIGWINCH = 27,  // Window size change
        SIGPOLL = 28,   // I/O now possible
        SIGSYS = 30,    // Bad system call.
    }

    [UnsupportedOSPlatform("windows")]
    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int UnixKill(int pid, UnixSignal signal);

    private enum WindowsSignal : uint
    {
        SIGINT = 0,     // Interrupt (CTRL + C)
        SIGBREAK = 1,   // Break     (CTRL + Break)
    }

    [SupportedOSPlatform("windows")]
    [LibraryImport("windows-kill", EntryPoint = "?sendSignal@WindowsKillLibrary@@YAXKK@Z", SetLastError = true)]
    private static partial void WindowsKill(uint pid, WindowsSignal signal);

    private static void NativeKill(int pid, WindowsSignal windowsSignal, UnixSignal unixSignal)
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsKill((uint) pid, windowsSignal);
            return;
        }

        UnixKill(pid, unixSignal);

    }

    #endregion
}
