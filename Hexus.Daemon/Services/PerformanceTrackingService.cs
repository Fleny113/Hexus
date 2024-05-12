using Hexus.Daemon.Configuration;

namespace Hexus.Daemon.Services;

internal partial class PerformanceTrackingService(
    ILogger<PerformanceTrackingService> logger,
    HexusConfiguration configuration,
    ProcessStatisticsService processStatisticsService) : BackgroundService
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
                processStatisticsService.RefreshCpuUsage();
            }
            catch (Exception ex)
            {
                LogFailedRefresh(logger, ex);
            }
        }
    }

    [LoggerMessage(LogLevel.Warning, "Disabling the CPU performance tracking. An invalid interval ({interval}s) was passed in.")]
    private static partial void LogDisablePerformanceTracking(ILogger logger, double interval);

    [LoggerMessage(LogLevel.Error, "There was an error getting the updated CPU usage")]
    private static partial void LogFailedRefresh(ILogger logger, Exception ex);
}
