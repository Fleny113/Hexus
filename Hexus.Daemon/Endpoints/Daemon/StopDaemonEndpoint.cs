using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Daemon;

internal sealed class StopDaemonEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/daemon/stop")]
    public static NoContent Handle(
        [FromServices] HexusConfiguration configuration,
        [FromServices] ProcessManagerService processManagerService,
        [FromServices] IHostApplicationLifetime hostLifecycle)
    {
        // Stop ASP.NET from accepting requests.
        hostLifecycle.StopApplication();
        HexusLifecycle.DaemonStoppingTokenSource.Cancel();
        File.Delete(configuration.UnixSocket);

        // We can't let the IHostApplicationLifetime handle this by itself because systemd will send SIGHUP otherwise since the CLI will have stopped, but we still need to finish working
        HexusLifecycle.StopApplications(processManagerService);

        return TypedResults.NoContent();
    }
}
