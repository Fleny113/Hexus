using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hexus.Daemon.Endpoints;

public sealed class SendInputToApplicationEndpoint(ProcessManagerService _processManager) : IEndpoint
{
    [HttpMapPost("/{id:int}/stdin")]
    public Results<NoContent, NotFound, StatusCodeHttpResult, BadRequest<object>> Handle(int id, HexusSendInputRequest req)
    {
        // TODO: use FluentValidation
        if (!_processManager.IsApplicationRunning(id))
            return TypedResults.BadRequest<object>(new { Error = "The application is not running" });

        if (!_processManager.SendToApplication(id, req.Text, req.AddNewLine))
            return TypedResults.StatusCode(500);

        return TypedResults.NoContent();
    }
}

public sealed record HexusSendInputRequest(string Text, bool AddNewLine = true);
