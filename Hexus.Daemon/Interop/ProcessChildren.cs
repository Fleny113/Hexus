using System.Runtime.Versioning;
using Windows.Win32.System.Diagnostics.ToolHelp;
using Win32 = Windows.Win32;

namespace Hexus.Daemon.Interop;

internal static class ProcessChildren
{
    public static IEnumerable<ProcessInfo> GetProcessChildrenInfo(int parentId)
    {
        // Windows 5.1.2600 is Windows XP, which is the first version that supports Toolhelp32Snapshot
        //  while we could simply use Windows 10 for these checks,
        //  it does make sense to use the same versions as Microsoft.Windows.CsWin32 defines
        if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
            return GetChildProcessesWindows((uint)parentId);

        if (OperatingSystem.IsLinux())
            return GetChildProcessesLinux(parentId);

        throw new NotSupportedException("Getting the child processes is not supported on this platform");
    }

    [SupportedOSPlatform("windows5.1.2600")]
    private static IEnumerable<ProcessInfo> GetChildProcessesWindows(uint parentId)
    {
        // While the docs says that this 0 will make the snapshot to start from the current process, since we use SnapProcess, it doesn't.
        using var processSnap = Win32.PInvoke.CreateToolhelp32Snapshot_SafeHandle(CREATE_TOOLHELP_SNAPSHOT_FLAGS.TH32CS_SNAPPROCESS, 0);

        if (processSnap.IsInvalid)
            yield break;

        HashSet<uint> parents = [parentId];

        var processEntity = new PROCESSENTRY32();
        unsafe
        {
            processEntity.dwSize = (uint)sizeof(PROCESSENTRY32);
        }

        if (!Win32.PInvoke.Process32First(processSnap, ref processEntity))
            yield break;

        do
        {
            if (!parents.Contains(processEntity.th32ParentProcessID))
                continue;

            parents.Add(processEntity.th32ProcessID);

            yield return new ProcessInfo
            {
                ProcessId = (int)processEntity.th32ProcessID,
                ParentProcessId = (int)processEntity.th32ParentProcessID,
            };
        } while (Win32.PInvoke.Process32Next(processSnap, ref processEntity));
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

