using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class StartApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/{name}/start")]
    public static Results<NoContent, NotFound, NotFound<object>, UnprocessableEntity> Handle(
        [FromRoute] string name,
        [FromServices] HexusConfigurationManager configManager, 
        [FromServices] ProcessManagerService processManager)
    {
        if (processManager.IsApplicationRunning(name))
            return TypedResults.NotFound(Constants.ApplicationIsRunningMessage);

        if (!configManager.Configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        if (!processManager.StartApplication(application))
            return TypedResults.UnprocessableEntity();

        configManager.SaveConfiguration();

        return TypedResults.NoContent();
    }
}
