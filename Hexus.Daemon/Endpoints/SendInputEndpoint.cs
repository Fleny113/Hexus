using EndpointMapper;
using FluentValidation;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints;

public sealed class SendInputEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/{id:int}/stdin")]
    public static Results<NoContent, NotFound<object>, BadRequest, ValidationProblem> Handle(
        [FromRoute] int id, 
        [FromBody] SendInputRequest request,
        [FromServices] ProcessManagerService processManager, 
        [FromServices] IValidator<SendInputRequest> validator)
    {
        var context = validator.Validate(request);

        if (!context.IsValid)
            return TypedResults.ValidationProblem(context.ToDictionary());

        if (!processManager.IsApplicationRunning(id))
            return TypedResults.NotFound(Constants.ApplicationIsNotRunningMessage);

        if (!processManager.SendToApplication(id, request.Text, request.AddNewLine))
            return TypedResults.BadRequest();

        return TypedResults.NoContent();
    }
}

public sealed record SendInputRequest(string Text, bool AddNewLine = true);
