using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hexus.Daemon;

internal partial class ProcessSignals
{
    public static int NativeSendSignal(int pid, WindowsSignal windowsSignal, UnixSignal unixSignal)
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsSendSignal((uint) pid, windowsSignal);
            return 0;
        }

        return UnixSendSignal(pid, unixSignal);
    }
    
    [UnsupportedOSPlatform("windows")]
    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int UnixSendSignal(int pid, UnixSignal signal);
    
    [SupportedOSPlatform("windows")]
    [LibraryImport("windows-kill", EntryPoint = "?sendSignal@WindowsKillLibrary@@YAXKK@Z", SetLastError = true)]
    private static partial void WindowsSendSignal(uint pid, WindowsSignal signal);
}

public enum UnixSignal
{
    SIGHUP = 1,     // Hangup
    SIGINT = 2,     // Interrupt 
    SIGQUIT = 3,    // Quit
    SIGILL = 4,     // Illegal instruction 
    SIGTRAP = 5,    // Trace trap 
    SIGABRT = 6,    // Abort 
    SIGBUS = 7,     // BUS error
    SIGFPE = 8,     // Floating-point exception
    SIGKILL = 9,    // UnixKill
    SIGUSR1 = 10,   // User-defined signal 1
    SIGSEGV = 11,   // Segmentation violation
    SIGUSR2 = 12,   // User-defined signal 2 
    SIGPIPE = 13,   // Broken pipe
    SIGALRM = 14,   // Alarm clock
    SIGTERM = 15,   // Termination 
    SIGCHLD = 16,   // Child status has changed
    SIGCONT = 17,   // Continue
    SIGSTOP = 18,   // Stop
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
    SIGSYS = 30     // Bad system call.
}

public enum WindowsSignal : uint
{
    SIGINT = 0,     // Interrupt (CTRL + C)
    SIGBREAK = 1    // Break     (CTRL + Break)
}
