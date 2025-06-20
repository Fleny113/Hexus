using EndpointMapper;
using Hexus.Daemon.Configuration;
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
        [FromServices] ProcessStatisticsService processStatisticsService,
        [FromServices] HexusConfiguration configuration,
        [FromRoute] string name,
        [FromQuery] bool forceStop = false)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        processStatisticsService.StopTrackingApplicationUsage(application);

        processManager.AbortProcessRestart(application);
        processManager.StopApplication(application, forceStop);

        processStatisticsService.TrackApplicationUsages(application);

        var startError = processManager.StartApplication(application);

        if (startError is not null)
        {
            processStatisticsService.StopTrackingApplicationUsage(application);
            return TypedResults.BadRequest(new GenericFailureResponse(startError.Value.MapToErrorString()));
        }

        return TypedResults.NoContent();
    }
}
