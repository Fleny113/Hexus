using Hexus.Daemon.Configuration;

namespace Hexus.Daemon.Services;

internal sealed class HexusLifecycle(HexusConfigurationManager configManager, ProcessManagerService processManager) : IHostedLifecycleService
{
    public static bool IsDaemonStopped { get; private set; }
    
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        foreach (var application in configManager.Configuration.Applications.Values.Where(application =>
                     application is { Status: HexusApplicationStatus.Operating })) 
            processManager.StartApplication(application);

        Task.Run(async () =>
        {
            var interval = TimeSpan.FromSeconds(2);
            
            var timer = new PeriodicTimer(interval);
            var process = configManager.Configuration.Applications.Values.ElementAt(0).Process!;
            
            var previousTotalProcessorTime = process.TotalProcessorTime;
            
            while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
            {
                var currentTotalProcessorTime = process.TotalProcessorTime;
                var processorTimeDifference = currentTotalProcessorTime - previousTotalProcessorTime;
                previousTotalProcessorTime = currentTotalProcessorTime;
                
                var cpuUsage = processorTimeDifference / Environment.ProcessorCount / interval;
                var cpuPercentage = Math.Round(cpuUsage * 100);
                
                Console.WriteLine($"[{DateTime.Now}] The CPU usage is: {cpuPercentage:F0}% [{processorTimeDifference}]");
            }
        }, cancellationToken);
        
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
