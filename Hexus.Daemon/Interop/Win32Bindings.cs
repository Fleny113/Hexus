using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hexus.Daemon.Interop;

[SupportedOSPlatform("windows")]
internal static partial class Win32Bindings
{
    #region Process Children

    private const int StringMaxSize = 260;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessId);

#pragma warning disable SYSLIB1054
    // SYSLIB1054: Mark the method 'Process32First' with 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/ Invoke marshalling code at compile time 
    // ProcessEntry32 is not compatible with LibraryImport as it has a string to marshal.

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 processEntry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 processEntry);

#pragma warning restore SYSLIB1054

    #endregion

    #region Process Signals

    public static IntPtr CtrlRoutineProduceAddress;

    public static bool InitializeCtrlRoutineProcedureAddress()
    {
        // https://www.geoffchappell.com/studies/windows/win32/kernel32/api/index.htm?tx=48
        CtrlRoutineProduceAddress = GetProcedureAddress("kernel32", "CtrlRoutine");
        return CtrlRoutineProduceAddress != IntPtr.Zero;
    }

    private static IntPtr GetProcedureAddress(string module, string proc)
    {
        var modulePtr = GetModuleHandle(module);

        return modulePtr == IntPtr.Zero
            ? IntPtr.Zero
            : GetProcAddress(modulePtr, proc);
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleA", SetLastError = true)]
    private static partial IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPStr)] string moduleName);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GetProcAddress(IntPtr module, [MarshalAs(UnmanagedType.LPStr)] string procName);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial IntPtr OpenProcess(
        uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        uint dwProcessId
    );

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial IntPtr CreateRemoteThread(
        IntPtr hProcess,
        IntPtr lpThreadAttributes,
        uint dwStackSize,
        IntPtr lpStartAddress,
        uint lpParameter,
        uint dwCreationFlags,
        out uint lpThreadId
    );

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial uint WaitForSingleObject(IntPtr thread, uint milliseconds);

    #endregion

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(IntPtr snapshot);

    // https://learn.microsoft.com/en-us/windows/win32/api/tlhelp32/ns-tlhelp32-processentry32#requirements
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct ProcessEntry32()
    {
        public uint dwSize = (uint)Marshal.SizeOf<ProcessEntry32>();
        // Not in use. Always set to 0
        public uint cntUsage;
        public uint th32ProcessID;
        // Not in use. Always set to 0
        public IntPtr th32DefaultHeapID;
        // Not in use. Always set to 0
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        // Not in use. Always set to 0
        public long pcPriClassBase;
        // Not in use. Always set to 0
        public uint dwFlags;
        [MarshalAs(UnmanagedType.LPStr, SizeConst = StringMaxSize)]
        public string szExeFile = string.Empty;
    }
}

// https://learn.microsoft.com/en-us/windows/console/generateconsolectrlevent#parameters
public enum WindowsCtrlType : uint
{
    CtrlC = 0,
    CtrlBreak = 1,
}
