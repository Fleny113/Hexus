using Hexus.Daemon.Extensions;
using System.ComponentModel;
using System.Diagnostics;
using YamlDotNet.Serialization;

namespace Hexus.Daemon.Configuration;

public sealed record HexusApplication
{
    public required string Name { get; set; }
    public required string Executable { get; set; }

    [DefaultValue("")] public string Arguments { get; set; } = "";
    [DefaultValue("")] public string WorkingDirectory { get; set; } = "";
    public HexusApplicationStatus Status { get; set; } = HexusApplicationStatus.Exited;

    #region Internal proprieties
    [YamlIgnore] internal Process? Process { get; set; }
    [YamlIgnore] internal StreamWriter? LogFile { get; set; }
    
    // CPU performance tracking 
    [YamlIgnore] internal Dictionary<int, CpuStats> CpuStatsMap { get; } = new();
    [YamlIgnore] internal double TotalCpuUsage => Math.Clamp(Math.Round(GetCpuUsage() + GetChildrenCpuUsage(), 2), 0, 100);
    
    internal record struct CpuStats()
    {
        public TimeSpan LastTotalProcessorTime = TimeSpan.Zero;
        public DateTimeOffset LastGetProcessCpuUsageInvocation = DateTimeOffset.UtcNow;
    }
    
    #endregion
    
    private double GetCpuUsage()
    {
        if (Process is null)
            return 0.0d;
        
        var cpuStats = CpuStatsMap.GetValueOrDefault(Process.Id, new CpuStats());
                
        var cpuPercentage = Process.GetProcessCpuUsage(ref cpuStats);
        CpuStatsMap[Process.Id] = cpuStats;

        return cpuPercentage;
    }
    
    private double GetChildrenCpuUsage()
    {
        if (Process is null)
            return 0.0d;

        var children = Process.GetChildProcesses().ToArray();

        // For the killed children we don't care about tracking their CPU usages
        foreach (var key in CpuStatsMap.Keys.Except(children.Select(child => child.Id)))
            CpuStatsMap.Remove(key);
        
        var totalUsage = children
            .Select(child =>
            {
                var childCpuStats = CpuStatsMap.GetValueOrDefault(child.Id, new CpuStats());
                var cpuPercentage = child.GetProcessCpuUsage(ref childCpuStats);

                CpuStatsMap[child.Id] = childCpuStats;

                return cpuPercentage;
            })                
            .Aggregate((acc, curr) => acc + curr);
        
        return totalUsage;
    }
}
