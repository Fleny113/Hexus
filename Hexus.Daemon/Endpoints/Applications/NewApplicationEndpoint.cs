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

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class NewApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Post, "/new")]
    public static Results<Ok<ApplicationResponse>, BadRequest<GenericFailureResponse>, ValidationProblem> Handle(
        [FromBody] NewApplicationRequest request,
        [FromServices] IValidator<NewApplicationRequest> validator,
        [FromServices] HexusConfigurationManager configManager,
        [FromServices] ProcessManagerService processManager,
        [FromServices] ProcessStatisticsService processStatisticsService,
        [FromServices] ProcessLogsService processLogsService)
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
        processLogsService.RegisterApplication(application);

        var startError = processManager.StartApplication(application);
        
        if (startError is not null)
        {
            processStatisticsService.StopTrackingApplicationUsage(application);
            processLogsService.UnregisterApplication(application);

            return TypedResults.BadRequest(new GenericFailureResponse(startError.Value.MapToErrorString()));
        }

        configManager.Configuration.Applications.Add(application.Name, application);
        configManager.SaveConfiguration();

        var stats = processStatisticsService.GetApplicationStats(application);

        return TypedResults.Ok(application.MapToResponse(stats));
    }
}
