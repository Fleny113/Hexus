using EndpointMapper;
using Hexus.Configuration;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class RestartApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Patch, "/{name}/restart")]
    public static Results<NoContent, NotFound, BadRequest<GenericFailureResponse>> Handle(
        [FromServices] ProcessManagerService processManager,
        [FromServices] HexusConfigurationManager configuration,
        [FromRoute] string name,
        [FromQuery] bool forceStop = false)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        processManager.StopApplication(application, forceStop);

        var startError = processManager.StartApplication(application);

        if (startError is not null)
        {
            return TypedResults.BadRequest(new GenericFailureResponse(startError.Value.MapToErrorString()));
        }

        return TypedResults.NoContent();
    }
}
