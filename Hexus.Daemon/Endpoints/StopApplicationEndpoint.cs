using EndpointMapper;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints;

internal sealed class StopApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/{name}")]
    public static Results<NoContent, NotFound<ErrorResponse>> Handle(
        [FromServices] ProcessManagerService processManager,
        [FromRoute] string name,
        [FromQuery] bool forceStop = false)
    {
        if (!processManager.StopApplication(name, forceStop))
            return TypedResults.NotFound(ErrorResponses.ApplicationIsNotRunningMessage);

        return TypedResults.NoContent();
    }
}
