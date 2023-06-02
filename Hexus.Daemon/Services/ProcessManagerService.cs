using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Hexus.Daemon.Services;

public partial class ProcessManagerService(ILogger<ProcessManagerService> logger, IOptions<HexusConfiguration> options)
{
    private readonly ConcurrentDictionary<int, Process> _processes = new();

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
        process.Exited += HandleProcessExited;

        // Wait for the process to start (with a timeout of 30 seconds)
        if (!SpinWait.SpinUntil(() => process.Id is > 0, TimeSpan.FromSeconds(30)))
            return false;

        _processes[application.Id] = process;

        return true;
    }

    /// <summary>Stop the instance of an application</summary>
    /// <param name="id">The id of the application to stop</param>
    /// <returns>Whatever if the application was stopped or not</returns>
    public bool StopApplication(int id)
    {
        if (!_processes.TryGetValue(id, out var process))
            return false;

        KillProcessCore(process);

        if (!_processes.TryRemove(id, out _))
            return false;

        return true;
    }

    public bool IsApplicationRunning(int id) => _processes.ContainsKey(id);


    /// <summary>Send a message into the Standard Input (STDIN) of an application</summary>
    /// <param name="id">The id of the application</param>
    /// <param name="text">The text to send into the STDIN</param>
    /// <param name="newLine">Whatever or not to append an endline to the text</param>
    /// <returns>Whatever or not if the operation was successful or not</returns>
    public bool SendToApplication(int id, ReadOnlySpan<char> text, bool newLine = true)
    {
        if (!_processes.TryGetValue(id, out var process))
            return false;

        if (newLine)
            process.StandardInput.WriteLine(text);
        else
            process.StandardInput.Write(text);

        return true;
    }

    internal int GetApplicationId() 
    {
        int key = 0;

        while (true)
        {
            if (!_processes.ContainsKey(++key))
                return key;
        }
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
        if (sender is not Process process)
            return;

        logger.LogInformation("{PID} says: '{OutputData}'", process.Id, e.Data);
    }

    private void HandleProcessExited(object? sender, EventArgs e)
    {
        if (sender is not Process process)
            return;

        logger.LogInformation("{PID} has exited with code: {ExitCode}", process.Id, process.ExitCode);
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Start all application managed by Hexus on startup after the WebHost has started
    /// </summary>
    internal void ApplicationStartup()
    {
        foreach (var application in options.Value.Applications)
        {
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
