using Hexus.Configuration;
using Hexus.Daemon.Contracts;

namespace Hexus.Daemon.Services;

internal sealed class HexusLifecycle(
    HexusConfigurationManager configManager,
    ProcessManagerService processManager,
    StateManagerService stateManagerService) : IHostedService, IConfigRelodable
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var application in configManager.Applications.Values)
        {
            var persistantState = stateManagerService.LoadApplicationState(application);

            if (!application.Enabled || (persistantState is not null && persistantState.Crashed)) continue;

            processManager.StartApplication(application);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ReloadResult ReloadConfiguration(ConfigurationDiff diff)
    {
        List<string> warnings = [];

        if (diff.OldConfiguration.HttpPort != diff.NewConfiguration.HttpPort)
        {
            warnings.Add($"The HTTP port has changed from {diff.OldConfiguration.HttpPort} to {diff.NewConfiguration.HttpPort}. The change will not take effect until the daemon is restarted.");
        }

        if (diff.OldConfiguration.UnixSocket != diff.NewConfiguration.UnixSocket)
        {
            warnings.Add($"The Unix socket path has changed from {diff.OldConfiguration.UnixSocket} to {diff.NewConfiguration.UnixSocket}. The change will not take effect until the daemon is restarted.");
        }

        return new ReloadResult([], warnings, []);
    }
}
