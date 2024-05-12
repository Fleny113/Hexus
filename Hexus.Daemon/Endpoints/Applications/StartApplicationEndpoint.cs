using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class StartApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/{name}")]
    public static Results<NoContent, NotFound, ValidationProblem, StatusCodeHttpResult> Handle(
        [FromRoute] string name,
        [FromServices] ProcessManagerService processManager,
        [FromServices] HexusConfiguration configuration)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        if (processManager.IsApplicationRunning(application, out _))
            return TypedResults.ValidationProblem(ErrorResponses.ApplicationAlreadyRunning);

        if (!processManager.StartApplication(application))
            return TypedResults.StatusCode((int)HttpStatusCode.InternalServerError);

        return TypedResults.NoContent();
    }
}
