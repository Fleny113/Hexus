using EndpointMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class GetConfigurationEndpoint : IEndpoint
{
    [HttpMapGet("/")]
    public Ok<HexusConfiguration> Handle(IOptions<HexusConfiguration> options) => TypedResults.Ok(options.Value);
}
