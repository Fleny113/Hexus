using EndpointMapper;
using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class StartApplicationEndpoint : IEndpoint
{
    public static void Register(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/{name}", Handle);
    }

    private static Results<NoContent, NotFound, ValidationProblem, BadRequest<GenericFailureResponse>> Handle(
        [AsParameters] Parameters parameters,
        [FromServices] ProcessManagerService processManager,
        [FromServices] HexusConfigurationManager configuration
    )
    {
        if (!configuration.Applications.TryGetValue(parameters.Name, out var application)) return TypedResults.NotFound();

        if (processManager.IsApplicationProcessRunning(application, out _, out _)) return TypedResults.ValidationProblem(ErrorResponses.ApplicationAlreadyRunning);

        var startError = processManager.StartApplication(application);

        if (startError is not null) return TypedResults.BadRequest(new GenericFailureResponse(startError.Value.MapToErrorString()));

        return TypedResults.NoContent();
    }

    public record Parameters([FromRoute] string Name);
}
