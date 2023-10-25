using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints;

internal sealed class DeleteApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/{name}/delete")]
    public static Results<NoContent, NotFound> Handle(
        [FromServices] HexusConfigurationManager configManager,
        [FromServices] ProcessManagerService processManager,
        [FromRoute] string name,
        [FromQuery] bool forceStop = false)
    {
        if (!configManager.Configuration.Applications.ContainsKey(name))
            return TypedResults.NotFound();

        processManager.StopApplication(name, forceStop);

        configManager.Configuration.Applications.Remove(name);
        configManager.SaveConfiguration();

        return TypedResults.NoContent();
    }
}
