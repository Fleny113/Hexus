using EndpointMapper;
using FluentValidation;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class NewApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/new")]
    public static Results<Ok<ApplicationResponse>, ValidationProblem, StatusCodeHttpResult> Handle(
        [FromBody] NewApplicationRequest request,
        [FromServices] IValidator<NewApplicationRequest> validator,
        [FromServices] HexusConfigurationManager configManager,
        [FromServices] ProcessManagerService processManager,
        [FromServices] ProcessStatisticsService processStatisticsService)
    {
        // Fill some defaults that are not compile time constants, so they require to be filled in here.
        request = request with
        {
            WorkingDirectory = request.WorkingDirectory ?? EnvironmentHelper.Home,
            EnvironmentVariables = request.EnvironmentVariables ?? [],
        };

        if (!validator.Validate(request, out var validationResult))
            return TypedResults.ValidationProblem(validationResult.ToDictionary());

        var application = request.MapToApplication();

        if (configManager.Configuration.Applications.TryGetValue(application.Name, out _))
            return TypedResults.ValidationProblem(ErrorResponses.ApplicationAlreadyExists);

        processStatisticsService.TrackApplicationUsages(application);

        if (!processManager.StartApplication(application))
        {
            processStatisticsService.StopTrackingApplicationUsage(application);
            return TypedResults.StatusCode((int)HttpStatusCode.InternalServerError);
        }

        configManager.Configuration.Applications.Add(application.Name, application);
        configManager.SaveConfiguration();

        var stats = processStatisticsService.GetApplicationStats(application);

        return TypedResults.Ok(application.MapToResponse(stats));
    }
}
