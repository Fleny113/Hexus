using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class DeleteApplicationEndpoint(IOptions<HexusConfiguration> options, ProcessManagerService processManager) : IEndpoint
{
    [HttpMapDelete("/{id:int}/delete")]
    public Results<NoContent, NotFound> Handle(int id)
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
