using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class StopApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/{id:int}/stop")]
    public static Results<NoContent, UnprocessableEntity, NotFound<object>> Handle(
        [FromRoute] int id,
        [FromServices] IOptions<HexusConfiguration> options, 
        [FromServices] ProcessManagerService processManager)
    {
        if (!processManager.IsApplicationRunning(id))
            return TypedResults.NotFound(Constants.ApplicationIsNotRunningMessage);

        if (!processManager.StopApplication(id))
            return TypedResults.UnprocessableEntity();

        options.Value.SaveConfigurationToDisk();

        return TypedResults.NoContent();
    }
}
