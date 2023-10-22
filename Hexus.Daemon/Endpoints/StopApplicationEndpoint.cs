using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints;

public sealed class StopApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/{name}/stop")]
    public static Results<NoContent, NotFound<object>> Handle(
        [FromRoute] string name,
        [FromQuery] bool forceStop,
        [FromServices] ProcessManagerService processManager)
    {
        if (!processManager.StopApplication(name, forceStop))
            return TypedResults.NotFound(Constants.ApplicationIsNotRunningMessage);

        return TypedResults.NoContent();
    }
}
