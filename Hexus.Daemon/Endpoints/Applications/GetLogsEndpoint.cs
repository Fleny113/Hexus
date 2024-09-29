using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class GetLogsEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Get, "/{name}/logs")]
    public static async Task<NotFound?> Handle(
        [FromServices] HexusConfiguration configuration,
        [FromServices] ProcessLogsService processLogsService,
        HttpContext context,
        [FromServices] IOptions<JsonOptions> jsonOptions,
        [FromRoute] string name,
        [FromQuery] DateTimeOffset? before = null,
        CancellationToken ct = default)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        // When the aspnet or the hexus cancellation token get cancelled it cancels this as well
        var combinedCtSource = CancellationTokenSource.CreateLinkedTokenSource(ct, HexusLifecycle.DaemonStoppingToken);

        var logs = processLogsService.GetLogs(application, before, combinedCtSource.Token);

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Headers.ContentType = "application/json; charset=utf-8";

        // We need to manually write the response body as ASP.NET won't send the header using TypedResults.Ok() or anything similar

        await context.Response.WriteAsync("[", ct);
        await context.Response.Body.FlushAsync(ct);

        await foreach (var item in logs)
        {
            await context.Response.WriteAsync(JsonSerializer.Serialize(item, jsonOptions.Value.JsonSerializerOptions), cancellationToken: ct);
            await context.Response.WriteAsync(",", ct);
            await context.Response.Body.FlushAsync(ct);
        }

        await context.Response.WriteAsync("]", ct);
        await context.Response.Body.FlushAsync(ct);

        return null;
    }
}
