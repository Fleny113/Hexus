using EndpointMapper;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Services;
using Hexus.Daemon.Validators;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class SendInputEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/{name}/stdin")]
    public static Results<NoContent, Conflict<ErrorResponse>, ValidationProblem> Handle(
        [FromRoute] string name,
        [FromBody] SendInputRequest request,
        [FromServices] ProcessManagerService processManager)
    {
        if (!request.ValidateContract(out var errors))
            return TypedResults.ValidationProblem(errors);

        if (!processManager.SendToApplication(name, request.Text, request.AddNewLine))
            return TypedResults.Conflict(ErrorResponses.ApplicationIsNotRunningMessage);

        return TypedResults.NoContent();
    }
}
