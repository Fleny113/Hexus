namespace Hexus.Daemon.Interop;

internal static class ProcessSignals
{
    // https://learn.microsoft.com/en-us/windows/win32/procthread/process-security-and-access-rights
    private const uint CreateThreadDesiredAccess = 0x0400 | 0x0008 | 0x0010 | 0x0020 | 0x0002;
    // https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-waitforsingleobject#return-value
    // https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/596a1078-e883-4972-9bbc-49e60bebca55
    private const long WaitObject0 = 0x00000000;

    public static int NativeSendSignal(int pId, WindowsCtrlType windowsSignal, UnixSignal unixSignal)
    {
        if (!OperatingSystem.IsWindows())
            return UnixInterop.SendSignal(pId, unixSignal);

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
}
