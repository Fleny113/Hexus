using EndpointMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class UpdateConfigurationEndpoint : IEndpoint
{
    [HttpMapPost("/")]
    public NoContent Handle(UpdateConfiguration body, IOptions<HexusConfiguration> options)
    {
        options.Value.Localhost = body.OnlyLocalhost;
        options.Value.SaveConfigurationToDisk();

        return TypedResults.NoContent();
    }

    public sealed record UpdateConfiguration(bool OnlyLocalhost);
}
