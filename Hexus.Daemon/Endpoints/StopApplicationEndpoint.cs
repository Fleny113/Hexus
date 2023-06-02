using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hexus.Daemon.Endpoints;

public sealed class StopApplicationEndpoint(ProcessManagerService processManager) : IEndpoint
{
    [HttpMapDelete("/{id:int}/stop")]
    public Results<NoContent, UnprocessableEntity, NotFound<object>> Handle(int id)
    {
        if (!processManager.IsApplicationRunning(id))
            return TypedResults.NotFound(Constants.ApplicationIsNotRunningMessage);

        if (!processManager.StopApplication(id))
            return TypedResults.UnprocessableEntity();

        return TypedResults.NoContent();
    }
}
