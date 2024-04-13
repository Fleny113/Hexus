using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class ListApplicationsEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Get, "/list")]
    public static Ok<IEnumerable<HexusApplicationResponse>> Handle([FromServices] HexusConfiguration config)
        => TypedResults.Ok(config.Applications.MapToResponse());
}
