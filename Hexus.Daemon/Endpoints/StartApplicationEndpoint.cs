using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class StartApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/{id:int}/start")]
    public static Results<NoContent, NotFound, NotFound<object>, UnprocessableEntity> Handle(
        [FromRoute] int id,
        [FromServices] IOptions<HexusConfiguration> options, 
        [FromServices] ProcessManagerService processManager)
    {
        if (processManager.IsApplicationRunning(id))
            return TypedResults.NotFound(Constants.ApplicationIsRunningMessage);

        var application = options.Value.Applications.Find(x => x.Id == id);

        if (application is null)
            return TypedResults.NotFound();

        if (!processManager.StartApplication(application))
            return TypedResults.UnprocessableEntity();

        options.Value.SaveConfigurationToDisk();

        return TypedResults.NoContent();
    }
}
