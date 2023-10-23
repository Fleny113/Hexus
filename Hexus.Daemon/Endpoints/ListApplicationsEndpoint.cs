using EndpointMapper;
using Hexus.Daemon.Configuration;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

internal sealed class ListApplicationsEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Get, "/list")]
    public static Ok<Dictionary<string, HexusApplication>> Handle([FromServices] HexusConfiguration config) 
        => TypedResults.Ok(config.Applications);
}
