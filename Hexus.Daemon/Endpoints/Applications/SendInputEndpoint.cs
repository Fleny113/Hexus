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
    [HttpMap(HttpMapMethod.Post, "/{name}/stdin")]
    public static Results<NoContent, NotFound, ValidationProblem> Handle(
        [FromRoute] string name,
        [FromBody] SendInputRequest request,
        [FromServices] ProcessManagerService processManager,
        [FromServices] HexusConfigurationManager configuration)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        if (string.IsNullOrEmpty(request.Text))
            return TypedResults.ValidationProblem([new("Text", ["Text cannot be empty."])]);

        if (!processManager.SendToApplication(application, request.Text, request.AddNewLine))
            return TypedResults.ValidationProblem(ErrorResponses.ApplicationNotRunning);

        return TypedResults.NoContent();
    }
}
