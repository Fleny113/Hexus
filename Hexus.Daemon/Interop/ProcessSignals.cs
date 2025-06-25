using Windows.Win32.System.Console;
using Windows.Win32.System.Threading;
using Win32 = Windows.Win32;

namespace Hexus.Daemon.Interop;

internal static class ProcessSignals
{
    public static int SendSignal(int pId, WindowsCtrlType windowsSignal, UnixSignal unixSignal)
    {
        if (!OperatingSystem.IsWindows())
            return UnixInterop.SendSignal(pId, unixSignal);

        // We only support sending signals on Windows 7 and later, as the CtrlRoutine procedure is not available on earlier versions.
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            return -1;
        }

        if (Win32.CtrlRoutine.ProcedureAddress.IsNull)
        {
            return -1;
        }

        using var process = Win32.PInvoke.OpenProcess_SafeHandle(
            dwDesiredAccess: PROCESS_ACCESS_RIGHTS.PROCESS_CREATE_THREAD,
            bInheritHandle: false,
            dwProcessId: (uint)pId);

        if (process.IsInvalid)
        {
            return -1;
        }

        // Microsoft.Windows.CsWin32 creates CreateRemoteThread with pointer parameters, so we need to use unsafe
        unsafe
        {
            var lpStartRoutine = Win32.CtrlRoutine.ProcedureAddress.CreateDelegate<LPTHREAD_START_ROUTINE>();
            var lpParameter = (void*)(uint)windowsSignal;

            using var remoteThread = Win32.PInvoke.CreateRemoteThread(
                hProcess: process,
                lpThreadAttributes: null,
                dwStackSize: 0,
                lpStartAddress: lpStartRoutine,
                lpParameter: lpParameter,
                dwCreationFlags: 0,
                lpThreadId: null);

            if (remoteThread.IsInvalid)
                return -1;

            if (Win32.PInvoke.WaitForSingleObject(remoteThread, 0) != Win32.Foundation.WAIT_EVENT.WAIT_OBJECT_0)
                return -1;
        }

        return 0;
    }
}
