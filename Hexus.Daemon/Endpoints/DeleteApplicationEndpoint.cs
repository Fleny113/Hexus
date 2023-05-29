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
        if (!_options.Value.Applications.Exists(a => a.Id == id))
            return TypedResults.NotFound();

        _processManager.StopApplication(id);

        _options.Value.Applications.RemoveAll(a => a.Id == id);
        _options.Value.SaveConfigurationToDisk();

        return TypedResults.NoContent();
    }
}
