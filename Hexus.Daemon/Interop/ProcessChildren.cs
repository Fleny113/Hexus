using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;

namespace Hexus.Daemon.Interop;

internal class ProcessChildren
{
    [SupportedOSPlatform("windows")]
    public static Process[] GetChildProcessesWindows(int parentId)
    {
        var searcher = new ManagementObjectSearcher(queryString: $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId={parentId}");

        try
        {
            return searcher
                .Get()
                .OfType<ManagementObject>()
                .Select(obj => obj["ProcessId"].ToString() ?? "-1")
                .Where(pid => pid is not "-1")
                .Select(int.Parse)
                .Select(Process.GetProcessById)
                .ToArray();
        }
        catch (ArgumentException ex) when (ex.Message.EndsWith("is not running."))
        {
            return Array.Empty<Process>();
        }
    }

    [SupportedOSPlatform("linux")]
    public static Process[] GetChildProcessesLinux(int parentId)
    {
        try
        {
            return File.ReadAllText($"/proc/{parentId}/task/{parentId}/children")
                .Split(' ')
                .Select(int.Parse)
                .Select(Process.GetProcessById)
                .ToArray();
        }
        catch (ArgumentException ex) when (ex.Message.EndsWith("is not running."))
        {
            return Array.Empty<Process>();
        }
    }
}
