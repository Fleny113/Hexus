using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Services;
using Hexus.Daemon.Validators;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Hexus.Daemon.Endpoints;

internal sealed class NewApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/new")]
    public static Results<Ok<HexusApplicationResponse>, ValidationProblem, Conflict<ErrorResponse>, StatusCodeHttpResult> Handle(
        [FromBody] NewApplicationRequest request,
        [FromServices] HexusConfigurationManager configManager,
        [FromServices] ProcessManagerService processManager)
    {
        if (request.WorkingDirectory is "")
            request.WorkingDirectory = EnvironmentHelper.Home;
        
        if (!request.ValidateContract(out var errors))
            return TypedResults.ValidationProblem(errors);

        var application = request.MapToApplication();

        if (configManager.Configuration.Applications.TryGetValue(application.Name, out _))
            return TypedResults.Conflict(ErrorResponses.ApplicationWithTheSameNameAlreadyExiting);

        if (!processManager.StartApplication(application))
            return TypedResults.StatusCode((int)HttpStatusCode.InternalServerError);

        configManager.Configuration.Applications.Add(application.Name, application);
        configManager.SaveConfiguration();

        return TypedResults.Ok(application.MapToResponse());
    }
}

