using EndpointMapper;
using Hexus.Configuration;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Daemon;

internal class HealthEndpoint : IEndpoint, IRegisterEndpoint
{
    public static void Register(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/daemon/health", Handle);
    }

    public static Ok<ConfigurationProblems> Handle([FromServices] HexusConfigurationManager configurationManager)
    {
        return TypedResults.Ok(new ConfigurationProblems(configurationManager.Warnings, configurationManager.Errors));
    }
}
