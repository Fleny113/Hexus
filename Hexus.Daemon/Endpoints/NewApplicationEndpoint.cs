using EndpointMapper;
using FluentValidation;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class NewApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/new")]
    public static Results<Ok<HexusApplication>, ValidationProblem, UnprocessableEntity> Handle(
        [FromBody] NewApplicationRequest request,
        [FromServices] IOptions<HexusConfiguration> options,
        [FromServices] ProcessManagerService processManager,
        [FromServices] IValidator<NewApplicationRequest> validator)
    {
        var context = validator.Validate(request);

        if (!context.IsValid)
            return TypedResults.ValidationProblem(context.ToDictionary());

        var application = request.MapToApplication();

        application.Id = processManager.GetApplicationId();

        if (!processManager.StartApplication(application))
            return TypedResults.UnprocessableEntity();

        options.Value.Applications.Add(application);
        options.Value.SaveConfigurationToDisk();

        return TypedResults.Ok(application);
    }
}

public sealed record NewApplicationRequest(string Name, string Executable, string Arguments = "", string WorkingDirectory = "");
