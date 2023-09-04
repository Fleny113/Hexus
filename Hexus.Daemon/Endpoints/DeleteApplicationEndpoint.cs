using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class DeleteApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/{id:int}/delete")]
    public static Results<NoContent, NotFound> Handle(
        [FromRoute] int id,
        [FromServices] IOptions<HexusConfiguration> options,
        [FromServices] ProcessManagerService processManager) 
    {
        var application = options.Value.Applications.Find(a => a.Id == id);

        if (application is null)
            return TypedResults.NotFound();

        processManager.StopApplication(id);

        options.Value.Applications.Remove(application);
        options.Value.SaveConfigurationToDisk();

        return TypedResults.NoContent();
    }
}
