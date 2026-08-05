using EndpointMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Daemon;

internal sealed class StopDaemonEndpoint : IEndpoint
{
    public static void Register(IEndpointRouteBuilder builder)
    {
        builder.MapDelete("/daemon/stop", Handle);
    }

    private static NoContent Handle([FromServices] IHostApplicationLifetime hostLifecycle)
    {
        hostLifecycle.StopApplication();
        return TypedResults.NoContent();
    }
}
