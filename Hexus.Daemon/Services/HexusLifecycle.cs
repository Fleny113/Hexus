using Hexus.Daemon.Configuration;

namespace Hexus.Daemon.Services;

internal sealed class HexusLifecycle(HexusConfigurationManager configManager, ProcessManagerService processManager) : IHostedLifecycleService
{
    internal static readonly CancellationTokenSource DaemonStoppingTokenSource = new();
    public static CancellationToken DaemonStoppingToken => DaemonStoppingTokenSource.Token;
    public static bool IsDaemonStopped => DaemonStoppingTokenSource.IsCancellationRequested;

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        var runningApplications = configManager.Configuration.Applications.Values
            .Where(application => application is { Status: HexusApplicationStatus.Running });

        foreach (var application in runningApplications)
            processManager.StartApplication(application);

        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        File.Delete(configManager.Configuration.UnixSocket);
        StopApplications(processManager);

        return Task.CompletedTask;
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        DaemonStoppingTokenSource.Cancel();

        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static void StopApplications(ProcessManagerService processManagerService)
    {
        // We need to make sure where are only 1 call to this in parallel
        // Else we might try to stop applications that exiting
        lock (processManagerService)
        {
            Parallel.ForEach(processManagerService.Applications.Values,
                application => processManagerService.StopApplication(application.Name));
        }
    }
}
