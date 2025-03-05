using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class StartApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/{name}")]
    public static Results<NoContent, NotFound, ValidationProblem, BadRequest<GenericFailureResponse>> Handle(
        [FromRoute] string name,
        [FromServices] ProcessManagerService processManager,
        [FromServices] ProcessStatisticsService processStatisticsService,
        [FromServices] HexusConfiguration configuration)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        if (processManager.IsApplicationRunning(application, out _))
            return TypedResults.ValidationProblem(ErrorResponses.ApplicationAlreadyRunning);

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
