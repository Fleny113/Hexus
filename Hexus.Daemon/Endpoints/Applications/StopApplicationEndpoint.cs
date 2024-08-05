using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class StopApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/{name}")]
    public static Results<NoContent, NotFound, ValidationProblem> Handle(
        [FromServices] ProcessManagerService processManager,
        [FromServices] ProcessStatisticsService processStatisticsService,
        [FromServices] HexusConfigurationManager configurationManager,
        [FromRoute] string name,
        [FromQuery] bool forceStop = false)
    {
        if (!configurationManager.Configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        var stop = processManager.StopApplication(application, forceStop);
        var abort = processManager.AbortProcessRestart(application);

        if (stop)
            processStatisticsService.StopTrackingApplicationUsage(application);

        if (!stop && !abort)
            return TypedResults.ValidationProblem(ErrorResponses.ApplicationNotRunning);

        if (!stop && abort)
        {
            application.Status = HexusApplicationStatus.Exited;
            configurationManager.SaveConfiguration();
        }

        return TypedResults.NoContent();
    }
}
