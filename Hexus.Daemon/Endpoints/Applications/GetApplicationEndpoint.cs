using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class GetApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Get, "/{name}")]
    public static Results<Ok<ApplicationResponse>, NotFound> Handle(
        [FromRoute] string name,
        [FromServices] HexusConfiguration configuration,
        [FromServices] ProcessStatisticsService processStatisticsService)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        return TypedResults.Ok(application.MapToResponse(processStatisticsService.GetApplicationStats(application)));
    }
}
