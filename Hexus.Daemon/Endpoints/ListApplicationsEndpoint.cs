using EndpointMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class ListApplicationsEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Get, "/list")]
    public static Ok<List<HexusApplication>> Handle([FromServices] IOptions<HexusConfiguration> options) 
        => TypedResults.Ok(options.Value.Applications);
}
