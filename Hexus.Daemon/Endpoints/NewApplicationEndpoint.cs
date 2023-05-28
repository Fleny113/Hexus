using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class NewApplicationEndpoint(IOptions<HexusConfiguration> _options, ProcessManagerService _processManager) : IEndpoint
{
    [HttpMapPost("/new")]
    public Results<Ok<List<HexusApplication>>, StatusCodeHttpResult> Handle([FromBody] NewHexusApplicationRequest request)
    {
        var application = Mapper.RequestToApplication(request);

        if (!_processManager.StartApplication(application))
        {
            return TypedResults.StatusCode(500);
        }

        _options.Value.Applications.Add(application);
        _options.Value.SaveConfigurationToDisk();

        return TypedResults.Ok(_options.Value.Applications);
    }
}

public sealed record NewHexusApplicationRequest(string Name, string Executable, string Arguments = "", string WorkingDirectory = "");
