using Hexus.Daemon.Interop;
using Hexus.Daemon.Services;
using System.Diagnostics;

namespace Hexus.Daemon.Extensions;

internal static class ProcessExtensions
{
    public static double GetProcessCpuUsage(this Process process, ProcessStatisticsService.CpuStatistics cpuStatistics)
    {
        var currentTime = DateTimeOffset.UtcNow;
        var deltaTime = currentTime - cpuStatistics.LastTime;

        var totalProcessTime = process.TotalProcessorTime;
        var deltaProcessTime = totalProcessTime - cpuStatistics.LastTotalProcessorTime;

        var cpuUsage = deltaProcessTime / Environment.ProcessorCount / deltaTime;

        cpuStatistics.LastTotalProcessorTime = totalProcessTime;
        cpuStatistics.LastTime = currentTime;

        return cpuUsage * 100;
    }

    public static IEnumerable<Process> GetChildProcesses(this Process process)
    {
        return ProcessChildren.GetProcessChildrenInfo(process.Id)
            .Select(processInfo => Process.GetProcessById(processInfo.ProcessId));
    }
}
