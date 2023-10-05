using Hexus.Daemon.Configuration;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Hexus.Daemon.Services;

public partial class ProcessManagerService(ILogger<ProcessManagerService> logger, HexusConfigurationManager configManager)
{
    private readonly ConcurrentDictionary<string, Process> _processes = new();
    private readonly ConcurrentDictionary<Process, HexusApplication> _applications = new();

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
        process.OutputDataReceived += HandleDataReceived;
        process.ErrorDataReceived += HandleDataReceived;

        process.Exited += AcknowledgeProcessExit;
        process.Exited += HandleProcessRestart;

        // Wait for the process to start (with a timeout of 30 seconds)
        if (!SpinWait.SpinUntil(() => process.Id is > 0, TimeSpan.FromSeconds(30)))
            return false;

        application.Status = HexusApplicationStatus.Operating;

        _processes[application.Name] = process;
        _applications[process] = application;

        return true;
    }

    /// <summary>Stop the instance of an application</summary>
    /// <param name="id">The name of the application to stop</param>
    /// <returns>Whatever if the application was stopped or not</returns>
    public bool StopApplication(string id)
    {
        if (!_processes.TryGetValue(id, out var process))
            return false;

        if (!_applications.TryGetValue(process, out var application))
            return false;

        if (application is not { Status: HexusApplicationStatus.Operating })
            return false;

        // Remove the restart event handler, as it will restart the process as soon as it stops
        process.Exited -= HandleProcessRestart;

        KillProcessCore(process);

        application.Status = HexusApplicationStatus.Exited;

        process.Close();

        return true;
    }

    public bool IsApplicationRunning(string name) => _processes.ContainsKey(name);

    /// <summary>Send a message into the Standard Input (STDIN) of an application</summary>
    /// <param name="name">The name of the application</param>
    /// <param name="text">The text to send into the STDIN</param>
    /// <param name="newLine">Whatever or not to append an endline to the text</param>
    /// <returns>Whatever or not if the operation was successful or not</returns>
    public bool SendToApplication(string name, ReadOnlySpan<char> text, bool newLine = true)
    {
        if (!_processes.TryGetValue(name, out var process))
            return false;

        if (newLine)
            process.StandardInput.WriteLine(text);
        else
            process.StandardInput.Write(text);

        return true;
    }

    private void KillProcessCore(Process process)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                WindowsKill((uint)process.Id, WindowsSignals.SIGINT);
            else
                UnixKill(process.Id, UnixSignals.SIGINT);
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Error during the stop of a process");

            // Independently from the exception, stop the process forcefully
            process.Kill();
        }

        // Wait up to 30 seconds for the process to stop
        if (!SpinWait.SpinUntil(() => process.HasExited, TimeSpan.FromSeconds(30)))
        {
            // If after 30 seconds the process hasn't stopped, kill it forcefully
            process.Kill(true);
        }
    }

    #region Process Handlers

    private void HandleDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (sender is not Process process || !_applications.TryGetValue(process, out var application))
            return;

        logger.LogTrace("{PID} says: '{OutputData}'", process.Id, e.Data);

        lock (application)
        {
            File.AppendAllText($"{EnvironmentHelper.LogsDirectory}/{application.Name}.log", $"{e.Data}\n");
        }
    }

    #region Exit handlers

    private const int _maxRestarts = 10;

    // If the application can't live for more then 30 seconds, after the 10 attempts to restart it, it will be considerate crashed
    private static readonly TimeSpan _resetTimeWindow = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, (int Restarts, CancellationTokenSource Cts)> _consequentialRestarts = new();

    private void AcknowledgeProcessExit(object? sender, EventArgs e)
    {
        if (sender is not Process process || !_applications.TryGetValue(process, out var application))
            return;

        _processes.TryRemove(application.Name, out _);
        
        application.Status = HexusApplicationStatus.Exited;
        configManager.SaveConfiguration();

        logger.LogDebug("Acknowledging about \"{Name}\" exiting with code: {ExitCode} [{PID}]", application.Name, process.ExitCode, process.Id);
    }

    private void HandleProcessRestart(object? sender, EventArgs e)
    {
        if (sender is not Process process || !_applications.TryGetValue(process, out var application))
            return;

        // Fire and forget
        _ = HandleProcessRestartCoreAsync(application);
    }

    private async Task HandleProcessRestartCoreAsync(HexusApplication application)
    {
        if (_consequentialRestarts.TryGetValue(application.Name, out var tuple))
        {
            tuple.Restarts++;
            tuple.Cts.Dispose();
            tuple.Cts = new CancellationTokenSource(_resetTimeWindow);
        }
        else
            tuple = (1, new CancellationTokenSource(_resetTimeWindow));

        var (restarts, cts) = tuple;

        if (restarts >= _maxRestarts)
        {
            logger.LogWarning("Application \"{Name}\" has exited for {maxRestarts} times in the time window ({TimeWindow} seconds). It will be considered crashed", 
                application.Name, restarts, _resetTimeWindow.TotalSeconds);

            application.Status = HexusApplicationStatus.Crashed;

            cts.Dispose();
            _consequentialRestarts.TryRemove(application.Name, out _);

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

        tuple.Cts.Dispose();

        logger.LogDebug("After {Restarts} restarts, application \"{Name}\" stopped restarting", tuple.Restarts, name);
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

    #endregion

    #region Lifecycle

    /// <summary>
    /// Start all application managed by Hexus on startup after the WebHost has started
    /// </summary>
    internal void ApplicationStartup()
    {
        foreach (var (_, application) in configManager.Configuration.Applications)
        {
            if (application is { Status: not HexusApplicationStatus.Operating })
                continue;

            StartApplication(application);
        }
    }

    /// <summary>
    /// Gracefully shutdown all the applications after the WebHost has stopped
    /// </summary>
    internal void ApplicationShutdown()
    {
        foreach (var process in _processes)
        {
            StopApplication(process.Key);
        }
    }

    #endregion

    #region Interop (LibraryImport)

    private enum UnixSignals : int
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

    private enum WindowsSignals : uint
    {
        SIGINT = 0,     // Interrupt (CTRL + C)
        SIGBREAK = 1,   // Break     (CTRL + Break)
    }

    [UnsupportedOSPlatform("windows")]
    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int UnixKill(int pid, UnixSignals signal);

    [SupportedOSPlatform("windows")]
    [LibraryImport("windows-kill", EntryPoint = "?sendSignal@WindowsKillLibrary@@YAXKK@Z", SetLastError = true)]
    private static partial void WindowsKill(uint pid, WindowsSignals signal);

    #endregion
}
