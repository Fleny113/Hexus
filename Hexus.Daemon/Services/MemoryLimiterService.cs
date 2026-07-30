using Hexus.Configuration;
using Hexus.Daemon.Contracts;

namespace Hexus.Daemon.Services;

internal sealed partial class MemoryLimiterService(
    ILogger<MemoryLimiterService> logger,
    HexusConfigurationManager configuration,
    ProcessLogsService processLogsService,
    ProcessManagerService processManagerService) : BackgroundService, IConfigRelodable
{
    private readonly PeriodicTimer _timer = new(Timeout.InfiniteTimeSpan);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            _timer.Period = configuration.DaemonConfiguration.MemoryPollingInterval;
        }
        catch (Exception)
        {
            LogDisableMemoryLimiter(logger, configuration.DaemonConfiguration.MemoryPollingInterval);
        }

        while (!ct.IsCancellationRequested && await _timer.WaitForNextTickAsync(ct))
        {
            foreach (var (_, application) in configuration.Applications)
            {
                CheckApplicationMemoryUsage(application);
            }
        }
    }

    private void CheckApplicationMemoryUsage(ApplicationConfiguration application)
    {
        try
        {
            var memoryLimit = application.MemoryLimit;

            // A memory limit of 0 means no limit
            if (memoryLimit == 0) return;

            if (!processManagerService.IsApplicationProcessRunning(application, out _, out var process)) return;

            var memoryUsage = ProcessStatisticsService.GetMemoryUsage(process);

            if (memoryUsage < memoryLimit) return;

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

    public ReloadResult ReloadConfiguration(ConfigurationDiff diff)
    {
        var actions = new List<string>();
        var errors = new List<ConfigurationNotice>();

        if (diff.OldConfiguration.MemoryPollingInterval != diff.NewConfiguration.MemoryPollingInterval)
        {
            try
            {
                _timer.Period = configuration.DaemonConfiguration.MemoryPollingInterval;

                actions.Add("Updated memory polling interval");
            }
            catch (Exception)
            {
                errors.Add(new ConfigurationNotice(
                    $"Failed to update memory polling interval to {configuration.DaemonConfiguration.MemoryPollingInterval}. The value is invalid.",
                    "Memory Limiter"));
            }
        }

        if (diff.OldConfiguration.MemoryLimit != diff.NewConfiguration.MemoryLimit || diff.Modified.Any(t => t.Old.MemoryLimit != t.New.MemoryLimit))
        {
            actions.Add("Updated memory limits for applications");
        }

        return new ReloadResult(actions, [], errors);
    }

    [LoggerMessage(LogLevel.Warning, "Disabling the memory limiter. An invalid interval ({interval}) was passed.")]
    private static partial void LogDisableMemoryLimiter(ILogger logger, TimeSpan interval);

    [LoggerMessage(LogLevel.Error, "An error occurred with application {name} memory limit check/enforcement")]
    private static partial void LogFailedCheck(ILogger logger, Exception ex, string name);
}
