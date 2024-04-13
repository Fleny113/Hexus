using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hexus.Daemon.Interop;

internal static class ProcessChildren
{
    [SupportedOSPlatform("windows")]
    public static IEnumerable<Process> GetChildProcessesWindows(int parentId)
    {
        // While the docs says that this 0 will make the snapshot to start from the current process, since we use SnapProcess, it doesn't.
        var processSnap = Win32Bindings.CreateToolhelp32Snapshot(Win32Bindings.Th32CsSnapProcess, 0);
        if (processSnap == IntPtr.Zero) yield break;

        var processEntity = new Win32Bindings.ProcessEntry32
        {
            dwSize = (uint)Marshal.SizeOf<Win32Bindings.ProcessEntry32>(),
            szExeFile = "",
        };

        var parents = new Stack<uint>();

        try
        {
            if (!Win32Bindings.Process32First(processSnap, ref processEntity)) yield break;

            do
            {
                if (processEntity.th32ParentProcessID != parentId && !parents.Contains(processEntity.th32ParentProcessID)) continue;

                parents.Push(processEntity.th32ProcessID);
                yield return Process.GetProcessById((int)processEntity.th32ProcessID);
            } while (Win32Bindings.Process32Next(processSnap, ref processEntity));
        }
        finally
        {
            Win32Bindings.CloseHandle(processSnap);
        }
    }

    [SupportedOSPlatform("linux")]
    public static IEnumerable<Process> GetChildProcessesLinux(int parentId)
    {
        foreach (var strPId in File.ReadAllText($"/proc/{parentId}/task/{parentId}/children").Split(' '))
        {
            if (!int.TryParse(strPId, out var processId)) continue;

            yield return Process.GetProcessById(processId);
            foreach (var childProcessId in GetChildProcessesLinux(processId)) yield return childProcessId;
        }
    }

    [SupportedOSPlatform("windows")]
    [SuppressMessage(category: "Interoperability",
        checkId:
        "SYSLIB1054:Use \'LibraryImportAttribute\' instead of \'DllImportAttribute\' to generate P/Invoke marshalling code at compile time",
        Justification = "If it was possible to use LibraryImportAttribute with the ProcessEntry32 i would use it.")]
    internal static class Win32Bindings
    {
        private const int MaxSize = 260;
        public const int Th32CsSnapProcess = 0x00000002;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 processEntry);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 processEntry);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr snapshot);

        // https://learn.microsoft.com/en-us/windows/win32/api/tlhelp32/ns-tlhelp32-processentry32#requirements
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct ProcessEntry32
        {
            public uint dwSize;
            // Not in use. Always set to 0
            public uint cntUsage;
            public uint th32ProcessID;
            // Not in use. Always set to 0
            public nint th32DefaultHeapID;
            // Not in use. Always set to 0
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            // Not in use. Always set to 0
            public int pcPriClassBase;
            // Not in use. Always set to 0
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxSize)]
            public string szExeFile;
        }
    }
}
