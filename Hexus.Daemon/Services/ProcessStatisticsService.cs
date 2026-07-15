using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Interop;
using System.Diagnostics;

namespace Hexus.Daemon.Services;

internal sealed class ProcessStatisticsService(ProcessManagerService processManagerService)
{
    private readonly Dictionary<ApplicationConfiguration, ApplicationCpuStatistics> _cpuStatisticsMap = [];

    public ApplicationStatistics GetApplicationStats(ApplicationConfiguration application)
    {
        if (
            !processManagerService.IsApplicationProcessRunning(application, out var state, out var process) ||
            !_cpuStatisticsMap.TryGetValue(application, out var cpuStatistics)
        )
        {
            return new ApplicationStatistics(TimeSpan.Zero, 0, ApplicationStatus.Stopped, 0, 0);
        }

        return new ApplicationStatistics(
            ProcessUptime: DateTime.Now - process.StartTime,
            ProcessId: process.Id,
            Status: state.Status,
            CpuUsage: cpuStatistics.LastUsage,
            MemoryUsage: GetMemoryUsage(application)
        );
    }

    public void TrackApplicationUsages(ApplicationConfiguration application)
    {
        _cpuStatisticsMap[application] = new ApplicationCpuStatistics();
    }

    public bool StopTrackingApplicationUsage(ApplicationConfiguration application)
    {
        return _cpuStatisticsMap.Remove(application, out _);
    }

    internal void RefreshCpuUsage()
    {
        var children = ProcessChildren.GetProcessChildrenInfo(Environment.ProcessId)
            .GroupBy(x => x.ParentProcessId)
            .ToDictionary(x => x.Key, x => x.Select(inf => inf.ProcessId));

        if (!children.TryGetValue(Environment.ProcessId, out var hexusChildren)) return;

        var liveApplications = _cpuStatisticsMap.Keys
            .Select(app => (IsRunning: processManagerService.IsApplicationProcessRunning(app, out _, out var process), Application: app, Process: process))
            .Where(tuple => tuple.IsRunning && hexusChildren.Contains(tuple.Process!.Id))
            .ToDictionary(tuple => tuple.Process!.Id, t => t.Application);

        foreach (var child in hexusChildren)
        {
            if (!liveApplications.TryGetValue(child, out var application)) continue;
            if (!_cpuStatisticsMap.TryGetValue(application, out var statistics)) continue;

            var processes = Traverse(child, children);
            var cpuUsage = GetApplicationCpuUsage(statistics, processes).Sum();
            statistics.LastUsage = Math.Clamp(Math.Round(cpuUsage, 2), 0, 100);
        }
    }

    internal long GetMemoryUsage(ApplicationConfiguration application)
    {
        if (!processManagerService.IsApplicationProcessRunning(application, out _, out var process))
            return 0;

        return GetApplicationProcesses(process)
            .Where(proc => proc is { HasExited: false })
            .Sum(proc =>
            {
                proc.Refresh();

                return OperatingSystem.IsWindows()
                   ? proc.PrivateMemorySize64
                   : proc.WorkingSet64;
            });
    }

    #region Refresh CPU Internals

    private static IEnumerable<Process> Traverse(int processId, IReadOnlyDictionary<int, IEnumerable<int>> processIds)
    {
        yield return Process.GetProcessById(processId);

        if (!processIds.TryGetValue(processId, out var childrenIds)) yield break;

        foreach (var child in childrenIds)
        {
            foreach (var childProc in Traverse(child, processIds))
            {
                yield return childProc;
            }
        }
    }

    private static IEnumerable<double> GetApplicationCpuUsage(ApplicationCpuStatistics statistics, IEnumerable<Process> processes)
    {
        // We need to cache the processes into an array, because we will be iterating over them multiple times, and we don't want to re-enumerate the IEnumerable each time.
        var enumerableProcesses = processes.ToArray();

        var deathChildren = statistics.ProcessCpuStatistics.Keys.Except(enumerableProcesses.Select(x => x.Id));

        // For death
        foreach (var processId in deathChildren)
        {
            statistics.ProcessCpuStatistics.Remove(processId);
        }

        // For newly spawned children and for exiting ones
        foreach (var process in enumerableProcesses)
        {
            var stats = statistics.ProcessCpuStatistics.GetOrCreate(process.Id, _ => new CpuStatistics
            {
                LastTotalProcessorTime = TimeSpan.Zero,
                LastTime = DateTimeOffset.UtcNow,
            });

            yield return process.GetProcessCpuUsage(stats);
        }
    }

    #endregion

    private IEnumerable<Process> GetApplicationProcesses(Process parent)
    {
        var children = ProcessChildren.GetProcessChildrenInfo(parent.Id);

        yield return parent;

        foreach (var child in children)
        {
            yield return Process.GetProcessById(child.ProcessId);
        }
    }

    private record ApplicationCpuStatistics
    {
        public Dictionary<int, CpuStatistics> ProcessCpuStatistics { get; } = [];
        public double LastUsage { get; set; }
    }

    internal record CpuStatistics
    {
        public TimeSpan LastTotalProcessorTime { get; set; }
        public DateTimeOffset LastTime { get; set; }
    }
}

internal record ApplicationStatistics(
    TimeSpan ProcessUptime,
    long ProcessId,
    ApplicationStatus Status,
    double CpuUsage,
    long MemoryUsage
);
