using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class StartApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/{name}/start")]
    public static Results<NoContent, NotFound, NotFound<object>, UnprocessableEntity> Handle(
        [FromRoute] string name,
        [FromServices] ProcessManagerService processManager)
    {
        if (processManager.IsApplicationRunning(name, out var application))
            return TypedResults.NotFound(Constants.ApplicationIsRunningMessage);

        if (application is null || !processManager.StartApplication(application))
            return TypedResults.UnprocessableEntity();

        return TypedResults.NoContent();
    }
}
