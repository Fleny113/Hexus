using Hexus.Configuration;
using Hexus.Daemon.Contracts;

namespace Hexus.Daemon.Services;

internal partial class PerformanceTrackingService(
    ILogger<PerformanceTrackingService> logger,
    HexusConfigurationManager configuration,
    ProcessStatisticsService processStatisticsService) : BackgroundService, IConfigRelodable
{
    private readonly PeriodicTimer _timer = new(Timeout.InfiniteTimeSpan);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            _timer.Period = configuration.DaemonConfiguration.CpuPollingInterval;
        }
        catch (Exception)
        {
            LogDisablePerformanceTracking(logger, configuration.DaemonConfiguration.CpuPollingInterval);
        }

        while (!ct.IsCancellationRequested && await _timer.WaitForNextTickAsync(ct))
        {
            try
            {
                processStatisticsService.RefreshCpuUsage();
            }
            catch (Exception ex)
            {
                LogFailedRefresh(logger, ex);
            }
        }
    }

    public ReloadResult ReloadConfiguration(ConfigurationDiff diff)
    {
        if (diff.OldConfiguration.CpuPollingInterval == diff.NewConfiguration.CpuPollingInterval) return new ReloadResult([], [], []);

        var actions = new List<string>();
        var errors = new List<ConfigurationNotice>();

        try
        {
            _timer.Period = configuration.DaemonConfiguration.CpuPollingInterval;

            actions.Add("Updated cpu polling interval");
        }
        catch (Exception)
        {
            errors.Add(new ConfigurationNotice($"Failed to update cpu polling interval to {configuration.DaemonConfiguration.CpuPollingInterval}. The value is invalid.",
                "Performance Tracking"));
        }

        return new ReloadResult(actions, [], errors);
    }

    [LoggerMessage(LogLevel.Warning, "Disabling the CPU performance tracking. An invalid interval ({interval}) was passed.")]
    private static partial void LogDisablePerformanceTracking(ILogger logger, TimeSpan interval);

    [LoggerMessage(LogLevel.Error, "An error occurred getting the updated CPU usage")]
    private static partial void LogFailedRefresh(ILogger logger, Exception ex);
}
