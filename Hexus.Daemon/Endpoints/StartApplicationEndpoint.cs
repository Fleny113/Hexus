using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class StartApplicationEndpoint(IOptions<HexusConfiguration> options, ProcessManagerService processManager) : IEndpoint
{
    [HttpMapPost("/{id:int}/start")]
    public Results<NoContent, NotFound, NotFound<object>, UnprocessableEntity> Handle(int id)
    {
        if (processManager.IsApplicationRunning(id))
            return TypedResults.NotFound(Constants.ApplicationIsRunningMessage);

        var application = options.Value.Applications.Find(x => x.Id == id);

        if (application is null)
            return TypedResults.NotFound();

        if (!processManager.StartApplication(application))
            return TypedResults.UnprocessableEntity();

        options.Value.SaveConfigurationToDisk();

        return TypedResults.NoContent();
    }
}
