using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hexus.Daemon.Interop;

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
    SigHup = 1,     // Hangup
    SigInt = 2,     // Interrupt
    SigQuit = 3,    // Quit
    SigIll = 4,     // Illegal instruction
    SigTrap = 5,    // Trace trap
    SigAbrt = 6,    // Abort
    SigBus = 7,     // BUS error
    SigFpe = 8,     // Floating-point exception
    SigKill = 9,    // UnixKill
    SigUsr1 = 10,   // User-defined signal 1
    SigSegv = 11,   // Segmentation violation
    SigUsr2 = 12,   // User-defined signal 2
    SigPipe = 13,   // Broken pipe
    SigAlrm = 14,   // Alarm clock
    SigTerm = 15,   // Termination
    SigChld = 16,   // Child status has changed
    SigCont = 17,   // Continue
    SigStop = 18,   // Stop
    SigTstp = 19,   // Keyboard stop
    SigTtin = 20,   // Background read from tty
    SigTtou = 21,   // Background write to tty
    SigUrg = 22,    // Urgent condition on socket
    SigXcpu = 23,   // CPU limit exceeded
    SigXfsz = 24,   // File size limit exceeded
    SigVtalrm = 25, // Virtual alarm clock
    SigProf = 26,   // Profiling alarm clock
    SigWinch = 27,  // Window size change
    SigPoll = 28,   // I/O now possible
    SigSys = 30     // Bad system call
}

public enum WindowsSignal : uint
{
    SigInt = 0,     // Interrupt (CTRL + C)
    SigBreak = 1    // Break     (CTRL + Break)
}
