using EndpointMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Daemon;

internal sealed class StopDaemonEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/daemon/stop")]
    public static NoContent Handle([FromServices] IHostApplicationLifetime hostLifecycle)
    {
        hostLifecycle.StopApplication();

        return TypedResults.NoContent();
    }
}
