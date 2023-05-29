using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hexus.Daemon.Endpoints;

public sealed class StopApplicationEndpoint(ProcessManagerService _processManager) : IEndpoint
{
    [HttpMapDelete("/{id:int}/stop")]
    public Results<NoContent, NotFound> Handle(int id)
    {
        if (!_processManager.StopApplication(id))
            return TypedResults.NotFound();

        return TypedResults.NoContent();
    }
}
