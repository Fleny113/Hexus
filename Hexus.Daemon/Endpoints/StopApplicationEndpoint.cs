using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints;

public sealed class StopApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/{name}/stop")]
    public static Results<NoContent, UnprocessableEntity, NotFound<object>> Handle(
        [FromRoute] string name,
        [FromServices] HexusConfigurationManager options, 
        [FromServices] ProcessManagerService processManager)
    {
        if (!processManager.IsApplicationRunning(name))
            return TypedResults.NotFound(Constants.ApplicationIsNotRunningMessage);

        if (!processManager.StopApplication(name))
            return TypedResults.UnprocessableEntity();

        options.SaveConfiguration();

        return TypedResults.NoContent();
    }
}
