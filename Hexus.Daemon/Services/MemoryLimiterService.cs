using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;

namespace Hexus.Daemon.Services;

internal sealed partial class MemoryLimiterService(
    ILogger<MemoryLimiterService> logger,
    HexusConfiguration configuration,
    ProcessStatisticsService processStatisticsService,
    ProcessLogsService processLogsService,
    ProcessManagerService processManagerService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(configuration.MemoryLimitCheckIntervalSeconds);

        if (interval.TotalMilliseconds is <= 0 or >= uint.MaxValue)
        {
            LogDisableMemoryLimiter(logger, configuration.MemoryLimitCheckIntervalSeconds);
            return;
        }

        if (configuration.MemoryLimit == 0)
        {
            LogConfigDisabledMemoryLimiter(logger);
            return;
        }

        var timer = new PeriodicTimer(interval);

        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            foreach (var (_, application) in configuration.Applications)
            {
                CheckApplicationMemoryUsage(application);
            }
        }
    }

    private void CheckApplicationMemoryUsage(HexusApplication application)
    {
        try
        {
            var memoryLimit = application.MemoryLimit ?? configuration.MemoryLimit;

            // A memory limit of 0 means no limit
            if (memoryLimit == 0)
            {
                return;
            }

            var memoryUsage = (ulong)processStatisticsService.GetMemoryUsage(application);

            if (memoryUsage < memoryLimit)
            {
                return;
            }

            processLogsService.ProcessApplicationLog(application,
                LogType.SYSTEM,
                $"Memory limit exceeded. Current usage: {memoryUsage} bytes. Limit: {memoryLimit} bytes. Killing the application.");

            processManagerService.KillApplication(application);
        }
        catch (Exception ex)
        {
            LogFailedCheck(logger, ex, application.Name);
        }
    }

    [LoggerMessage(LogLevel.Debug, "The memory limiter was disabled in the config file.")]
    private static partial void LogConfigDisabledMemoryLimiter(ILogger logger);

    [LoggerMessage(LogLevel.Warning, "Disabling the memory limiter. An invalid interval ({interval}s) was passed in.")]
    private static partial void LogDisableMemoryLimiter(ILogger logger, double interval);

    [LoggerMessage(LogLevel.Error, "There was an error with application {name} memory limit check/enforcement")]
    private static partial void LogFailedCheck(ILogger logger, Exception ex, string name);
}
