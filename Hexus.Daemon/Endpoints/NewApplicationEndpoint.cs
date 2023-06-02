using EndpointMapper;
using FluentValidation;
using Hexus.Daemon.Services;
using Hexus.Daemon.Validators;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace Hexus.Daemon.Endpoints;

public sealed class NewApplicationEndpoint(
    IOptions<HexusConfiguration> options, 
    ProcessManagerService processManager,
    IValidator<NewApplicationRequest> validator) : IEndpoint
{
    [HttpMapPost("/new")]
    public Results<Ok<HexusApplication>, ValidationProblem, UnprocessableEntity> Handle([FromBody] NewApplicationRequest request)
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

// Use FluentValidation to validate the request
public sealed record NewApplicationRequest(string Name, string Executable, string Arguments = "", string WorkingDirectory = "");
