using Hexus.Daemon.Configuration;

namespace Hexus.Daemon.Services;

internal sealed class HexusLifecycle(HexusConfigurationManager configManager, ProcessManagerService processManager) : IHostedLifecycleService
{
    private static readonly CancellationTokenSource DaemonStoppingTokenSource = new();
    public static CancellationToken DaemonStoppingToken => DaemonStoppingTokenSource.Token;
    public static bool IsDaemonStopped => DaemonStoppingTokenSource.IsCancellationRequested;
    
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        foreach (var application in configManager.Configuration.Applications.Values.Where(application =>
                     application is { Status: HexusApplicationStatus.Running })) 
            processManager.StartApplication(application);
        
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        File.Delete(configManager.Configuration.UnixSocket);
        
        foreach (var application in processManager.Applications.Values) 
            processManager.StopApplication(application.Name);

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
}
