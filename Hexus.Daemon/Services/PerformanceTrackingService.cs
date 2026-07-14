using Hexus.Configuration;

namespace Hexus.Daemon.Services;

internal partial class PerformanceTrackingService(
    ILogger<PerformanceTrackingService> logger,
    HexusConfigurationManager configuration,
    ProcessStatisticsService processStatisticsService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = configuration.DaemonConfiguration.CpuPollingInterval;

        if (interval.TotalMilliseconds is <= 0 or >= uint.MaxValue)
        {
            LogDisablePerformanceTracking(logger, configuration.DaemonConfiguration.CpuPollingInterval);
            return;
        }

        var timer = new PeriodicTimer(interval);

        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
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

    [LoggerMessage(LogLevel.Warning, "Disabling the CPU performance tracking. An invalid interval ({interval}) was passed.")]
    private static partial void LogDisablePerformanceTracking(ILogger logger, TimeSpan interval);

    [LoggerMessage(LogLevel.Error, "An error occurred getting the updated CPU usage")]
    private static partial void LogFailedRefresh(ILogger logger, Exception ex);
}
