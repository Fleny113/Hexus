using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class RestartApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Patch, "/{name}/restart")]
    public static Results<NoContent, NotFound, StatusCodeHttpResult> Handle(
        [FromServices] ProcessManagerService processManager,
        [FromServices] ProcessStatisticsService processStatisticsService,
        [FromServices] HexusConfiguration configuration,
        [FromRoute] string name,
        [FromQuery] bool forceStop = false)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        processStatisticsService.StopTrackingApplicationUsage(application);

        processManager.StopApplication(application, forceStop);

        processStatisticsService.TrackApplicationUsages(application);

        if (!processManager.StartApplication(application))
        {
            processStatisticsService.StopTrackingApplicationUsage(application);
            return TypedResults.StatusCode((int)HttpStatusCode.InternalServerError);
        }

        return TypedResults.NoContent();
    }
}
