using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class DeleteApplicationEndpoint(IOptions<HexusConfiguration> _options, ProcessManagerService _processManager) : IEndpoint
{
    [HttpMapDelete("/{id:int}/delete")]
    public Results<NoContent, NotFound> Handle(int id)
    {
        var application = _options.Value.Applications.Find(a => a.Id == id);

        if (application is null)
            return TypedResults.NotFound();

        _processManager.StopApplication(id);

        _options.Value.Applications.Remove(application);
        _options.Value.SaveConfigurationToDisk();

        return TypedResults.NoContent();
    }
}
