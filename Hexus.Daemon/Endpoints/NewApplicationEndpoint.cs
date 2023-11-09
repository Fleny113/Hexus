using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Services;
using Hexus.Daemon.Validators;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints;

internal sealed class NewApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/new")]
    public static Results<Ok<HexusApplicationResponse>, ValidationProblem, UnprocessableEntity, UnprocessableEntity<object>> Handle(
        [FromBody] NewApplicationRequest request,
        [FromServices] HexusConfigurationManager configManager,
        [FromServices] ProcessManagerService processManager)
    {
        if (request.WorkingDirectory is "")
            request.WorkingDirectory = EnvironmentHelper.Home;
        
        if (!request.ValidateContract(out var errors))
            return TypedResults.ValidationProblem(errors);

        var application = request.MapToApplication();

        if (configManager.Configuration.Applications.Values.FirstOrDefault(x => x.Name == application.Name) is not null)
            return TypedResults.UnprocessableEntity(Constants.ApplicationWithTheSameNameAlreadyExiting);

        if (!processManager.StartApplication(application))
            return TypedResults.UnprocessableEntity();

        configManager.Configuration.Applications.Add(application.Name, application);
        configManager.SaveConfiguration();

        return TypedResults.Ok(application.MapToResponse());
    }
}

