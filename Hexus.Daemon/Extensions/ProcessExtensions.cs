using Hexus.Daemon.Configuration;
using Hexus.Daemon.Interop;
using System.Diagnostics;

namespace Hexus.Daemon.Extensions;

internal static class ProcessExtensions
{
    public static double GetProcessCpuUsage(this Process process, HexusApplication.CpuStats cpuStats)
    {
        var currentTime = DateTimeOffset.UtcNow;
        var timeDifference = currentTime - cpuStats.LastGetProcessCpuUsageInvocation;

        // In a situation like this we are provably going to give an unreasonable number, it's better to just say 0% than 100%
        if (timeDifference < TimeSpan.FromMilliseconds(100))
            return 0.00;

        var currentTotalProcessorTime = process.TotalProcessorTime;
        var processorTimeDifference = currentTotalProcessorTime - cpuStats.LastTotalProcessorTime;

        var cpuUsage = processorTimeDifference / Environment.ProcessorCount / timeDifference;

        cpuStats.LastTotalProcessorTime = currentTotalProcessorTime;
        cpuStats.LastGetProcessCpuUsageInvocation = currentTime;

        return cpuUsage * 100;
    }

    public static IEnumerable<Process> GetChildProcesses(this Process process)
    {
        return ProcessChildren.GetProcessChildrenInfo(process.Id)
            .Select(processInfo => Process.GetProcessById(processInfo.ProcessId));
    }
}
