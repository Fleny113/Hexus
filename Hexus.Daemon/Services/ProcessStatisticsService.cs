using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Interop;
using System.Diagnostics;

namespace Hexus.Daemon.Services;

internal sealed class ProcessStatisticsService(ProcessManagerService processManagerService)
{
    private readonly Dictionary<int, ApplicationCpuStatistics> _cpuStatisticsMap = [];

    public ApplicationStatistics GetApplicationStats(ApplicationConfiguration application)
    {
        var state = processManagerService.GetApplicationState(application);

        if (!ProcessManagerService.IsApplicationProcessRunning(state, out var process))
        {
            return new ApplicationStatistics(
                ProcessUptime: TimeSpan.Zero,
                ProcessId: 0,
                Status: state.Status,
                CpuUsage: 0,
                MemoryUsage: 0
            );
        }

        // If an application is running, but doesn't have a cpu stats it means we haven't refreshed since it started
        var cpuUsage = _cpuStatisticsMap.TryGetValue(process.Id, out var cpuStatistics) ? cpuStatistics.LastUsage : 0;

        return new ApplicationStatistics(
            ProcessUptime: DateTime.Now - process.StartTime,
            ProcessId: process.Id,
            Status: state.Status,
            CpuUsage: cpuUsage,
            MemoryUsage: GetMemoryUsage(process)
        );
    }

    internal void RefreshCpuUsage()
    {
        var children = ProcessChildren.GetProcessChildrenInfo(Environment.ProcessId)
            .GroupBy(x => x.ParentProcessId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.ProcessId));

        if (!children.TryGetValue(Environment.ProcessId, out var hexusChildren)) return;

        var enumeratedHexusChilds = hexusChildren.ToArray();

        foreach (var pid in _cpuStatisticsMap.Keys.Except(enumeratedHexusChilds))
        {
            _cpuStatisticsMap.Remove(pid);
        }

        foreach (var child in enumeratedHexusChilds)
        {
            var statistics = _cpuStatisticsMap.GetOrCreate(child, _ => new ApplicationCpuStatistics());
            var cpuUsage = GetApplicationCpuUsage(statistics, Traverse(child, children)).Sum();

            statistics.LastUsage = Math.Clamp(Math.Round(cpuUsage, 2), 0, 100);
        }
    }

    internal static long GetMemoryUsage(Process process)
    {
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

    private static IEnumerable<Process> Traverse(int pid, IReadOnlyDictionary<int, IEnumerable<int>> processIds)
    {
        var process = Process.GetProcessById(pid);

        yield return process;

        if (!processIds.TryGetValue(process.Id, out var childrenIds)) yield break;

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

        // For death
        foreach (var processId in statistics.ProcessCpuStatistics.Keys.Except(enumerableProcesses.Select(p => p.Id)))
        {
            statistics.ProcessCpuStatistics.Remove(processId);
        }

        // For newly spawned children and for exiting ones
        foreach (var process in enumerableProcesses)
        {
            var stats = statistics.ProcessCpuStatistics.GetOrCreate(process.Id, _ => new ProcessExtensions.CpuStatistics());

            yield return process.GetProcessCpuUsage(stats);
        }
    }

    #endregion

    private static IEnumerable<Process> GetApplicationProcesses(Process parent)
    {
        var children = ProcessChildren.GetProcessChildrenInfo(parent.Id);

        yield return parent;

        foreach (var child in children)
        {
            yield return Process.GetProcessById(child.ProcessId);
        }
    }

    private class ApplicationCpuStatistics
    {
        public Dictionary<int, ProcessExtensions.CpuStatistics> ProcessCpuStatistics { get; } = [];
        public double LastUsage { get; set; }
    }
}

internal record ApplicationStatistics(
    TimeSpan ProcessUptime,
    long ProcessId,
    ApplicationStatus Status,
    double CpuUsage,
    long MemoryUsage
);
