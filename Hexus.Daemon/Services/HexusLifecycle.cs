using Hexus.Configuration;

namespace Hexus.Daemon.Services;

internal sealed class HexusLifecycle(
    HexusConfigurationManager configManager,
    ProcessManagerService processManager,
    ProcessLogsService processLogsService,
    ProcessStatisticsService processStatisticsService) : IHostedLifecycleService
{
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        foreach (var application in configManager.Applications.Values)
        {
            processLogsService.RegisterApplication(application);

            if (!application.Enabled) continue;

            processStatisticsService.TrackApplicationUsages(application);
            processManager.StartApplication(application);
        }

        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        StopApplications(processManager);
        foreach (var application in configManager.Applications.Values)
        {
            processStatisticsService.StopTrackingApplicationUsage(application);
        }

        return Task.CompletedTask;
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static void StopApplications(ProcessManagerService processManagerService)
    {
        // We need to make sure where are only 1 call to this in parallel
        // Else we might try to stop applications that are exiting
        lock (processManagerService)
        {
            processManagerService.StopApplications();
        }
    }
}
