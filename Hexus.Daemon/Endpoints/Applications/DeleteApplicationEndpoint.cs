using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class DeleteApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Delete, "/{name}/delete")]
    public static Results<NoContent, NotFound> Handle(
        [FromServices] HexusConfigurationManager configManager,
        [FromServices] ProcessManagerService processManager,
        [FromServices] ProcessStatisticsService processStatisticsService,
        [FromServices] ProcessLogsService processLogsService,
        [FromRoute] string name,
        [FromQuery] bool forceStop = false)
    {
        if (!configManager.Configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        processManager.StopApplication(application, forceStop);
        processStatisticsService.StopTrackingApplicationUsage(application);
        processLogsService.DeleteApplication(application);

        configManager.Configuration.Applications.Remove(name, out _);
        configManager.SaveConfiguration();

        return TypedResults.NoContent();
    }
}
