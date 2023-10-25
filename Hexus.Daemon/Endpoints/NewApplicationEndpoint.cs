using EndpointMapper;
using FluentValidation;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints;

internal sealed class NewApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/new")]
    public static Results<Ok<HexusApplication>, ValidationProblem, UnprocessableEntity> Handle(
        [FromBody] NewApplicationRequest request,
        [FromServices] HexusConfigurationManager configManager,
        [FromServices] ProcessManagerService processManager,
        [FromServices] IValidator<NewApplicationRequest> validator)
    {
        var context = validator.Validate(request);

        if (!context.IsValid)
            return TypedResults.ValidationProblem(context.ToDictionary());

        var application = request.MapToApplication();

        if (application.WorkingDirectory is "")
            application.WorkingDirectory = EnvironmentHelper.Home;

        if (!processManager.StartApplication(application))
            return TypedResults.UnprocessableEntity();

        configManager.Configuration.Applications.Add(application.Name, application);
        configManager.SaveConfiguration();

        return TypedResults.Ok(application);
    }
}

public sealed record NewApplicationRequest(string Name, string Executable, string Arguments = "", string WorkingDirectory = "");
