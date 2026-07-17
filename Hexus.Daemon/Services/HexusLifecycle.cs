using Hexus.Configuration;

namespace Hexus.Daemon.Services;

internal sealed class HexusLifecycle(
    HexusConfigurationManager configManager,
    ProcessManagerService processManager,
    ProcessLogsService processLogsService,
    StateManagerService stateManagerService) : IHostedLifecycleService
{
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        foreach (var application in configManager.Applications.Values)
        {
            processLogsService.RegisterApplication(application);

            var persistantState = stateManagerService.LoadApplicationState(application);

            if (!application.Enabled || (persistantState is not null && persistantState.Crashed)) continue;

            processManager.StartApplication(application);
        }

        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        StopApplications(processManager);

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
        // TODO: Check if we can work something our in the daemon stop endpoint to avoid this lock
        lock (processManagerService)
        {
            processManagerService.StopApplications();
        }
    }
}
