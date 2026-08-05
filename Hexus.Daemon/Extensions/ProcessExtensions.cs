using System.Diagnostics;

namespace Hexus.Daemon.Extensions;

internal static class ProcessExtensions
{
    internal record CpuStatistics
    {
        public TimeSpan LastTotalProcessorTime { get; set; } = TimeSpan.Zero;
        public DateTimeOffset LastTime { get; set; } = DateTimeOffset.UtcNow;
    }

    extension(Process process)
    {
        public double GetProcessCpuUsage(CpuStatistics cpuStatistics)
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
    }
}
