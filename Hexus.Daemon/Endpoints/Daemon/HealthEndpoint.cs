using EndpointMapper;
using Hexus.Configuration;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Daemon;

internal class HealthEndpoint : IEndpoint
{
    public static void Register(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/daemon/health", Handle);
    }

    private static Ok<ConfigurationProblems> Handle([FromServices] HexusConfigurationManager configurationManager)
    {
        return TypedResults.Ok(new ConfigurationProblems(configurationManager.Warnings, configurationManager.Errors));
    }
}
