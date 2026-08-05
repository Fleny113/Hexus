using EndpointMapper;
using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class StopApplicationEndpoint : IEndpoint
{
    public static void Register(IEndpointRouteBuilder builder)
    {
        builder.MapDelete("/{name}", Handle);
    }

    private static Results<NoContent, NotFound, ValidationProblem> Handle(
        [AsParameters] Parameters parameters,
        [FromServices] ProcessManagerService processManager,
        [FromServices] HexusConfigurationManager configurationManager)
    {
        if (!configurationManager.Applications.TryGetValue(parameters.Name, out var application)) return TypedResults.NotFound();

        var stop = processManager.StopApplication(application, parameters.ForceStop);

        if (!stop) return TypedResults.ValidationProblem(ErrorResponses.ApplicationNotRunning);

        return TypedResults.NoContent();
    }

    public record Parameters([FromRoute] string Name, [FromQuery] bool ForceStop = false);
}
