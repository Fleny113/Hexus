using EndpointMapper;
using FluentValidation;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class SendInputEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/{name}/stdin")]
    public static Results<NoContent, ValidationProblem> Handle(
        [FromRoute] string name,
        [FromBody] SendInputRequest request,
        [FromServices] IValidator<SendInputRequest> validator,
        [FromServices] ProcessManagerService processManager)
    {
        if (!validator.Validate(request, out var validationResult))
            return TypedResults.ValidationProblem(validationResult.ToDictionary());

        if (!processManager.SendToApplication(name, request.Text, request.AddNewLine))
            return TypedResults.ValidationProblem(ErrorResponses.ApplicationNotRunning);

        return TypedResults.NoContent();
    }
}
