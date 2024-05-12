using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class StopApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/{name}")]
    public static Results<NoContent, NotFound, ValidationProblem> Handle(
        [FromServices] ProcessManagerService processManager,
        [FromServices] ProcessStatisticsService processStatisticsService,
        [FromServices] HexusConfiguration configuration,
        [FromRoute] string name,
        [FromQuery] bool forceStop = false)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        if (!processManager.StopApplication(application, forceStop))
            return TypedResults.ValidationProblem(ErrorResponses.ApplicationNotRunning);

        processStatisticsService.StopTrackingApplicationUsage(application);

        return TypedResults.NoContent();
    }
}
