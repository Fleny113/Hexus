using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hexus.Daemon.Interop;

internal static partial class ProcessSignals
{
    // https://learn.microsoft.com/en-us/windows/win32/procthread/process-security-and-access-rights
    private const uint CreateThreadDesiredAccess = 0x0400 | 0x0008 | 0x0010 | 0x0020 | 0x0002;
    // https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-waitforsingleobject#return-value
    // https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/596a1078-e883-4972-9bbc-49e60bebca55
    private const long WaitObject0 = 0x00000000;

    public static int NativeSendSignal(int pId, WindowsCtrlType windowsSignal, UnixSignal unixSignal)
    {
        if (!OperatingSystem.IsWindows())
            return UnixSendSignal(pId, unixSignal);

        var process = Win32Bindings.OpenProcess(CreateThreadDesiredAccess, false, (uint)pId);

        var remoteThread = Win32Bindings.CreateRemoteThread(
            process,
            IntPtr.Zero,
            1024 * 1024,
            Win32Bindings.CtrlRoutinePointer,
            (uint)windowsSignal,
            0,
            out _);

        if (remoteThread == IntPtr.Zero)
        {
            Win32Bindings.CloseHandle(process);
            return -1;
        }

        if (Win32Bindings.WaitForSingleObject(remoteThread, 0) == WaitObject0) return 0;

        Win32Bindings.CloseHandle(process);
        Win32Bindings.CloseHandle(remoteThread);
        return -1;

    }

    [UnsupportedOSPlatform("windows")]
    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int UnixSendSignal(int pId, UnixSignal signal);
}

public enum UnixSignal
{
    SigHup = 1, // Hangup
    SigInt = 2, // Interrupt
    SigQuit = 3, // Quit
    SigIll = 4, // Illegal instruction
    SigTrap = 5, // Trace trap
    SigAbrt = 6, // Abort
    SigBus = 7, // BUS error
    SigFpe = 8, // Floating-point exception
    SigKill = 9, // UnixKill
    SigUsr1 = 10, // User-defined signal 1
    SigSegv = 11, // Segmentation violation
    SigUsr2 = 12, // User-defined signal 2
    SigPipe = 13, // Broken pipe
    SigAlrm = 14, // Alarm clock
    SigTerm = 15, // Termination
    SigChld = 16, // Child status has changed
    SigCont = 17, // Continue
    SigStop = 18, // Stop
    SigTstp = 19, // Keyboard stop
    SigTtin = 20, // Background read from tty
    SigTtou = 21, // Background write to tty
    SigUrg = 22, // Urgent condition on socket
    SigXcpu = 23, // CPU limit exceeded
    SigXfsz = 24, // File size limit exceeded
    SigVtalrm = 25, // Virtual alarm clock
    SigProf = 26, // Profiling alarm clock
    SigWinch = 27, // Window size change
    SigPoll = 28, // I/O now possible
    SigSys = 30, // Bad system call
}
