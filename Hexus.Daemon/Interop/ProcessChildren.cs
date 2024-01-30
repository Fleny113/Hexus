using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;

namespace Hexus.Daemon.Interop;

internal class ProcessChildren
{
    [SupportedOSPlatform("windows")]
    public static Process[] GetChildProcessesWindows(int parentId)
    {
        var searcher = new ManagementObjectSearcher($"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId={parentId}");

        try
        {
            return searcher
                .Get()
                .OfType<ManagementObject>()
                .Select(obj => obj["ProcessId"].ToString() ?? "-1")
                .Where(pid => pid is not "-1")
                .Select(ConvertToInt)
                .Where(pid => pid is not -1)
                .Select(Process.GetProcessById)
                .ToArray();
        }
        catch (ArgumentException ex) when (ex.Message.EndsWith("is not running."))
        {
            return [];
        }
    }

    [SupportedOSPlatform("linux")]
    public static Process[] GetChildProcessesLinux(int parentId)
    {
        try
        {
            return File.ReadAllText($"/proc/{parentId}/task/{parentId}/children")
                .Split(' ')
                .Select(ConvertToInt)
                .Where(pid => pid is not -1)
                .Select(Process.GetProcessById)
                .ToArray();
        }
        catch (ArgumentException ex) when (ex.Message.EndsWith("is not running."))
        {
            return [];
        }
    }

    private static int ConvertToInt(string value)
    {
        if (int.TryParse(value, out var parsed))
            return parsed;

        return -1;
    }
}
