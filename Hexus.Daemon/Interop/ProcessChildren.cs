using System.Runtime.Versioning;

namespace Hexus.Daemon.Interop;

internal static class ProcessChildren
{
    private const int Th32CsSnapProcess = 0x00000002;

    public static IEnumerable<ProcessInfo> GetProcessChildrenInfo(int parentId)
    {
        if (OperatingSystem.IsWindows())
            return GetChildProcessesWindows((uint)parentId);
        if (OperatingSystem.IsLinux())
            return GetChildProcessesLinux(parentId);

        throw new NotSupportedException("Getting the child processes is not supported on this platform");
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<ProcessInfo> GetChildProcessesWindows(uint parentId)
    {
        // While the docs says that this 0 will make the snapshot to start from the current process, since we use SnapProcess, it doesn't.
        var processSnap = Win32Bindings.CreateToolhelp32Snapshot(Th32CsSnapProcess, 0);
        if (processSnap == IntPtr.Zero) yield break;

        HashSet<uint> parents = [parentId];

        var processEntity = new Win32Bindings.ProcessEntry32();

        try
        {
            if (!Win32Bindings.Process32First(processSnap, ref processEntity)) yield break;

            do
            {
                if (!parents.Contains(processEntity.th32ParentProcessID)) continue;

                parents.Add(processEntity.th32ProcessID);
                yield return new ProcessInfo
                {
                    ProcessId = (int)processEntity.th32ProcessID,
                    ParentProcessId = (int)processEntity.th32ParentProcessID,
                };
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

            yield return new ProcessInfo
            {
                ProcessId = processId,
                ParentProcessId = parentId,
            };
            
            foreach (var childProcessId in GetChildProcessesLinux(processId))
            {
                yield return childProcessId;
            }
        }
    }

    public record struct ProcessInfo(int ProcessId, int ParentProcessId);
}

