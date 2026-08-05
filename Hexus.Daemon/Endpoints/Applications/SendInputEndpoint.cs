using EndpointMapper;
using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class SendInputEndpoint : IEndpoint
{
    public static void Register(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/{name}/stdin", Handle);
    }

    private static Results<NoContent, NotFound, ValidationProblem> Handle(
        [AsParameters] Parameters parameters,
        [FromServices] ProcessManagerService processManager,
        [FromServices] HexusConfigurationManager configuration)
    {
        if (!configuration.Applications.TryGetValue(parameters.Name, out var application))
            return TypedResults.NotFound();

        if (string.IsNullOrEmpty(parameters.Request.Text))
            return TypedResults.ValidationProblem([new("Text", ["Text cannot be empty."])]);

        if (!processManager.SendToApplication(application, parameters.Request.Text, parameters.Request.AddNewLine))
            return TypedResults.ValidationProblem(ErrorResponses.ApplicationNotRunning);

        return TypedResults.NoContent();
    }

    public record Parameters([FromRoute] string Name, [FromBody] SendInputRequest Request);
}
