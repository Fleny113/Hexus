using EndpointMapper;
using FluentValidation;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hexus.Daemon.Endpoints;

public sealed class SendInputEndpoint(ProcessManagerService processManager, IValidator<SendInputRequest> validator) : IEndpoint
{
    [HttpMapPost("/{id:int}/stdin")]
    public Results<NoContent, NotFound<object>, BadRequest, ValidationProblem> Handle(int id, SendInputRequest request)
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
