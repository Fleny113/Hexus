using EndpointMapper;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Hexus.Daemon.Endpoints;

internal sealed class StartApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/{name}")]
    public static Results<NoContent, Conflict<ErrorResponse>, StatusCodeHttpResult> Handle(
        [FromRoute] string name,
        [FromServices] ProcessManagerService processManager)
    {
        if (processManager.IsApplicationRunning(name, out var application))
            return TypedResults.Conflict(ErrorResponses.ApplicationIsRunningMessage);

        if (application is null || !processManager.StartApplication(application))
            return TypedResults.StatusCode((int)HttpStatusCode.InternalServerError);

        return TypedResults.NoContent();
    }
}
