using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class GetLogsEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Get, "/{name}/logs")]
    public static Results<Ok<IAsyncEnumerable<ApplicationLog>>, NotFound> Handle(
        [FromServices] HexusConfiguration configuration,
        [FromServices] ProcessLogsService processLogsService,
        [FromRoute] string name,
        [FromQuery] int lines = 100,
        [FromQuery] bool noStreaming = false,
        [FromQuery] bool currentExecution = false,
        [FromQuery] DateTimeOffset? before = null,
        [FromQuery] DateTimeOffset? after = null,
        CancellationToken ct = default)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        // When the aspnet or the hexus cancellation token get cancelled it cancels this as well
        var combinedCtSource = CancellationTokenSource.CreateLinkedTokenSource(ct, HexusLifecycle.DaemonStoppingToken);

        // If the before is in the past we can disable steaming
        if (before is not null && before < DateTimeOffset.UtcNow) noStreaming = true;

        var logs = processLogsService.GetLogs(application, lines, !noStreaming, currentExecution, before, after, combinedCtSource.Token);

        return TypedResults.Ok(logs);
    }
}
