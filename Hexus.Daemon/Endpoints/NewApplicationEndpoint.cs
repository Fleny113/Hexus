using EndpointMapper;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class NewApplicationEndpoint(IOptions<HexusConfiguration> _options, ProcessManagerService _processManager) : IEndpoint
{
    [HttpMapPost("/new")]
    public Results<Ok<HexusApplication>, StatusCodeHttpResult> Handle([FromBody] NewHexusApplicationRequest request)
    {
        var application = Mapper.RequestToApplication(request);

        application.Id = _processManager.GetApplicationId();

        if (!_processManager.StartApplication(application))
            return TypedResults.StatusCode(500);

        _options.Value.Applications.Add(application);
        _options.Value.SaveConfigurationToDisk();

        return TypedResults.Ok(application);
    }
}

// Use FluentValidation to validate the request
public record struct NewHexusApplicationRequest(string Name, string Executable, string Arguments = "", string WorkingDirectory = "");
