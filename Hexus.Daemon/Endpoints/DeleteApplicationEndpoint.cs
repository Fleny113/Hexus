using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class DeleteApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/{name}/delete")]
    public static Results<NoContent, NotFound> Handle(
        [FromRoute] string name,
        [FromServices] HexusConfigurationManager configManager,
        [FromServices] ProcessManagerService processManager)
    {
        if (!configManager.Configuration.Applications.ContainsKey(name))
            return TypedResults.NotFound();

        processManager.StopApplication(name);

        configManager.Configuration.Applications.Remove(name);
        configManager.SaveConfiguration();

        return TypedResults.NoContent();
    }
}
