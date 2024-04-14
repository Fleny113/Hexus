using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hexus.Daemon.Interop;

internal static partial class ProcessChildren
{
    public static IEnumerable<ProcessInfo> GetProcessChildrenInfo(int parentId)
    {
        if (OperatingSystem.IsWindows())
            return GetChildProcessesWindows(parentId);
        if (OperatingSystem.IsLinux())
            return GetChildProcessesLinux(parentId);

        throw new NotSupportedException("Getting the child processes is only supported on Windows and Linux");
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<ProcessInfo> GetChildProcessesWindows(int parentId)
    {
        // While the docs says that this 0 will make the snapshot to start from the current process, since we use SnapProcess, it doesn't.
        var processSnap = Win32Bindings.CreateToolhelp32Snapshot(Win32Bindings.Th32CsSnapProcess, 0);
        if (processSnap == IntPtr.Zero) yield break;

        var parents = new Stack<uint>();
        var processEntity = new Win32Bindings.ProcessEntry32
        {
            dwSize = (uint)Marshal.SizeOf<Win32Bindings.ProcessEntry32>(),
        };

        try
        {
            if (!Win32Bindings.Process32First(processSnap, ref processEntity)) yield break;

            do
            {
                if (processEntity.th32ParentProcessID != parentId && !parents.Contains(processEntity.th32ParentProcessID)) continue;

                parents.Push(processEntity.th32ProcessID);
                yield return new ProcessInfo { ProcessId = (int)processEntity.th32ProcessID, ParentProcessId = (int)processEntity.th32ParentProcessID};
            } while (Win32Bindings.Process32Next(processSnap, ref processEntity));
        }
        finally
        {
            Win32Bindings.CloseHandle(processSnap);
        }
    }

    [SupportedOSPlatform("linux")]
    private static IEnumerable<ProcessInfo> GetChildProcessesLinux(int parentId)
    {
        foreach (var strPId in File.ReadAllText($"/proc/{parentId}/task/{parentId}/children").Split(' '))
        {
            if (!int.TryParse(strPId, out var processId)) continue;

            yield return new ProcessInfo { ProcessId = processId, ParentProcessId = parentId };
            foreach (var childProcessId in GetChildProcessesLinux(processId)) yield return childProcessId;
        }
    }

    internal record struct ProcessInfo(int ProcessId, int ParentProcessId);

    [SupportedOSPlatform("windows")]
    [SuppressMessage("Interoperability", "SYSLIB1054:Use \'LibraryImportAttribute\' instead of \'DllImportAttribute\' to generate P/Invoke marshalling code at compile time", Justification = "LibraryImport with ProcessEntry32 does not work without a lot of work to make it happy and both CompileTime and RunTime")]
    internal static partial class Win32Bindings
    {
        private const int MaxSize = 260;
        public const int Th32CsSnapProcess = 0x00000002;

        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 processEntry);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 processEntry);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CloseHandle(IntPtr snapshot);

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

