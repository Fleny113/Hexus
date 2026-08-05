using EndpointMapper;
using Hexus.Configuration;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class GetApplicationEndpoint : IEndpoint
{
    public static void Register(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/{name}", Handle);
    }

    private static Results<Ok<ApplicationResponse>, NotFound> Handle(
        [AsParameters] Parameters parameters,
        [FromServices] HexusConfigurationManager configuration,
        [FromServices] ProcessStatisticsService processStatisticsService)
    {
        if (!configuration.Applications.TryGetValue(parameters.Name, out var application))
            return TypedResults.NotFound();

        return TypedResults.Ok(application.MapToResponse(processStatisticsService.GetApplicationStats(application)));
    }

    public record Parameters([FromRoute] string Name);
}
