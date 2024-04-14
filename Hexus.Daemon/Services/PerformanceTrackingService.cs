using Hexus.Daemon.Configuration;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Interop;
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
            try
            {
                RefreshCpuUsage();
            }
            catch (Exception ex)
            {
                LogFailedRefresh(logger, ex);
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

                return proc.WorkingSet64;
            })
            .Sum();
    }

    private void RefreshCpuUsage()
    {
        var children = ProcessChildren.GetProcessChildrenInfo(Environment.ProcessId)
            .GroupBy(x => x.ParentProcessId)
            .ToDictionary(x => x.Key, x => x.Select(inf => inf.ProcessId).ToArray());

        if (!children.TryGetValue(Environment.ProcessId, out var hexusChildren)) return;

        var apps = configuration.Applications.Values
            .Where(app => app is { Process.HasExited: false } && hexusChildren.Contains(app.Process.Id))
            .ToDictionary(x => x.Process!.Id, x => x);

        foreach (var child in hexusChildren)
        {
            if (!apps.TryGetValue(child, out var app)) continue;

            var cpuUsage = Traverse(child, children).Select(proc => GetProcessCpuUsage(app, proc)).Sum();
            app.LastCpuUsage = Math.Clamp(Math.Round(cpuUsage, 2), 0, 100);
        }
    }

    private static IEnumerable<Process> Traverse(int processId, IReadOnlyDictionary<int, int[]> processIds)
    {
        yield return Process.GetProcessById(processId);

        if (!processIds.TryGetValue(processId, out var childrenIds)) yield break;

        foreach (var child in childrenIds)
        {
            yield return Process.GetProcessById(child);
            foreach (var childProc in Traverse(child, processIds)) yield return childProc;
        }
    }

    private static double GetProcessCpuUsage(HexusApplication application, Process process)
    {
        if (process is not { HasExited: false, Id: var processId })
        {
            application.CpuStatsMap.Remove(process.Id);
            return 0;
        }

        var cpuStats = application.CpuStatsMap.GetOrCreate(processId, _ => new HexusApplication.CpuStats
        {
            LastTotalProcessorTime = TimeSpan.Zero,
            LastGetProcessCpuUsageInvocation = DateTimeOffset.UtcNow,
        });

        return process.GetProcessCpuUsage(cpuStats);
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

    [LoggerMessage(LogLevel.Error, "There was an error getting the updated CPU usage")]
    private static partial void LogFailedRefresh(ILogger logger, Exception ex);
}
