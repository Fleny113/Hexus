using EndpointMapper;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class StopApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/{name}")]
    public static Results<NoContent, ValidationProblem> Handle(
        [FromServices] ProcessManagerService processManager,
        [FromRoute] string name,
        [FromQuery] bool forceStop = false)
    {
        if (!processManager.StopApplication(name, forceStop))
            return TypedResults.ValidationProblem(ErrorResponses.ApplicationNotRunning);

        return TypedResults.NoContent();
    }
}
