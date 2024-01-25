using Hexus.Daemon.Configuration;
using Hexus.Daemon.Extensions;
using System.Diagnostics;

namespace Hexus.Daemon.Services;

internal partial class PerformanceTrackingService(ILogger<PerformanceTrackingService> logger, HexusConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(configuration.CpuRefreshIntervalSeconds);
        
        if (interval.TotalMilliseconds is <= 0 or >= uint.MaxValue)
        {
            LogDisablePerformanceTracking(logger, configuration.CpuRefreshIntervalSeconds);
            return;
        }
        
        var timer = new PeriodicTimer(interval);

        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            foreach (var application in configuration.Applications.Values)
            {
                try
                {
                    RefreshCpuUsage(application);
                }
                catch
                {
                    // We don't care, it will just retry later.
                }
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
                if (process is not { HasExited: false, Id: var processId })
                {
                    application.CpuStatsMap.Remove(process.Id);
                    return 0;
                }

                var cpuStats = application.CpuStatsMap.GetOrCreate(processId,
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
    
    [LoggerMessage(LogLevel.Warning, "Disabling the CPU performance tracking. An invalid interval ({interval}s) was passed in.")]
    private static partial void LogDisablePerformanceTracking(ILogger logger, double interval);
}
