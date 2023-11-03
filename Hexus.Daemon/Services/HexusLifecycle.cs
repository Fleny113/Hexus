﻿using Hexus.Daemon.Configuration;

namespace Hexus.Daemon.Services;

internal sealed class HexusLifecycle(HexusConfigurationManager configManager, ProcessManagerService processManager) : IHostedLifecycleService
{
    public static bool IsDaemonStopped { get; private set; }
    
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        foreach (var application in configManager.Configuration.Applications.Values.Where(application =>
                     application is { Status: HexusApplicationStatus.Operating })) 
            processManager.StartApplication(application);

        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        IsDaemonStopped = true;

        File.Delete(configManager.Configuration.UnixSocket);
        
        foreach (var application in processManager.Applications.Values) 
            processManager.StopApplication(application.Name);

        return Task.CompletedTask;
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}