using EndpointMapper;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Hexus.Daemon.Validators;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints;

internal sealed class SendInputEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/{name}/stdin")]
    public static Results<NoContent, NotFound<object>, ValidationProblem> Handle(
        [FromRoute] string name,
        [FromBody] SendInputRequest request,
        [FromServices] ProcessManagerService processManager)
    {
        if (!request.ValidateContract(out var errors))
            return TypedResults.ValidationProblem(errors);

        if (!processManager.SendToApplication(name, request.Text, request.AddNewLine))
            return TypedResults.NotFound(Constants.ApplicationIsNotRunningMessage);

        return TypedResults.NoContent();
    }
}
