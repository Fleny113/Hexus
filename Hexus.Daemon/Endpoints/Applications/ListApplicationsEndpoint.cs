using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class ListApplicationsEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Get, "/list")]
    public static Ok<IEnumerable<ApplicationResponse>> Handle(
        [FromServices] HexusConfiguration config,
        [FromServices] ProcessStatisticsService processStatisticsService)
    {
        return TypedResults.Ok(config.Applications.Values.MapToResponse(processStatisticsService.GetApplicationStats));
    }
}
