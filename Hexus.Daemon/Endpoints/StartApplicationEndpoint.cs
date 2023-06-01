using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class StartApplicationEndpoint(IOptions<HexusConfiguration> _options, ProcessManagerService _processManager) : IEndpoint
{
    [HttpMapPut("/{id:int}/start")]
    public Results<NoContent, NotFound, StatusCodeHttpResult, BadRequest<object>> Handle(int id)
    {
        // TODO: use FluentValidation
        if (_processManager.IsApplicationRunning(id))
            return TypedResults.BadRequest<object>(new { Error = "The application is already running" });

        var application = _options.Value.Applications.Find(x => x.Id == id);

        if (application is null)
            return TypedResults.NotFound();

        if (!_processManager.StartApplication(application))
            return TypedResults.StatusCode(500);

        return TypedResults.NoContent();
    }
}
