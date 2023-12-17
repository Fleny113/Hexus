using Hexus.Daemon.Configuration;
using Hexus.Daemon.Extensions;
using System.Diagnostics;

namespace Hexus.Daemon.Services;

internal class PerformanceTrackingService(HexusConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(configuration.CpuRefreshIntervalSeconds));

        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            foreach (var application in configuration.Applications.Values)
            {
                RefreshCpuUsage(application);
            }
        }
    }

    internal static long GetMemoryUsage(HexusApplication application)
    {
        if (application.Process is not { HasExited: false })
            return 0;

        return GetApplicationProcesses(application)
            .Where(proc => proc is { HasExited: false })
            .Select(proc =>
            {
                proc.Refresh();

                return OperatingSystem.IsWindows()
                    ? proc.PagedMemorySize64
                    : proc.WorkingSet64;
            })
            .Sum();
    }

    private static void RefreshCpuUsage(HexusApplication application)
    {
        var cpuUsages = GetApplicationProcesses(application)
            .Select(process =>
            {
                if (process is not { HasExited: false })
                {
                    application.CpuStatsMap.Remove(process.Id);
                    return 0;
                }

                var cpuStats = application.CpuStatsMap.GetOrCreate(process.Id,
                    _ => new HexusApplication.CpuStats
                    {
                        LastTotalProcessorTime = TimeSpan.Zero, LastGetProcessCpuUsageInvocation = DateTimeOffset.UtcNow,
                    });

                return process.GetProcessCpuUsage(cpuStats);
            })
            .Sum();

        application.LastCpuUsage = Math.Clamp(Math.Round(cpuUsages, 2), 0, 100);
    }

    private static IEnumerable<Process> GetApplicationProcesses(HexusApplication application)
    {
        // If the process is null or has exited then return an empty list
        if (application.Process is not { HasExited: false })
            return [];

        return [application.Process, ..application.Process.GetChildProcesses()];
    }
}
