using EndpointMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class ListApplicationsEndpoint(IOptions<HexusConfiguration> _options) : IEndpoint
{
    [HttpMapGet("/list")]
    public Ok<List<HexusApplication>> Handle() => TypedResults.Ok(_options.Value.Applications);
}
