using EndpointMapper;
using FluentValidation;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints;

public sealed class SendInputEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/{name}/stdin")]
    public static Results<NoContent, NotFound<object>, ValidationProblem> Handle(
        [FromRoute] string name, 
        [FromBody] SendInputRequest request,
        [FromServices] ProcessManagerService processManager, 
        [FromServices] IValidator<SendInputRequest> validator)
    {
        var context = validator.Validate(request);

        if (!context.IsValid)
            return TypedResults.ValidationProblem(context.ToDictionary());

        if (!processManager.SendToApplication(name, request.Text, request.AddNewLine))
            return TypedResults.NotFound(Constants.ApplicationIsNotRunningMessage);

        return TypedResults.NoContent();
    }
}

public sealed record SendInputRequest(string Text, bool AddNewLine = true);
