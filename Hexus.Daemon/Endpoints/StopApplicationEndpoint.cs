using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hexus.Daemon.Endpoints;

public sealed class StopApplicationEndpoint(ProcessManagerService _processManager) : IEndpoint
{
    [HttpMapDelete("/{id:int}/stop")]
    public Results<NoContent, NotFound, BadRequest<object>> Handle(int id)
    {
        // TODO: use FluentValidation
        if (!_processManager.IsApplicationRunning(id))
            return TypedResults.BadRequest<object>(new { Error = "The application is not running" });

        if (!_processManager.StopApplication(id))
            return TypedResults.NotFound();

        return TypedResults.NoContent();
    }
}
