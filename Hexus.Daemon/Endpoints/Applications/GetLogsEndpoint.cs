using EndpointMapper;
using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Json;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class GetLogsEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Get, "/{name}/logs")]
    public static async Task<Results<NotFound, JsonArrayStreamResult<ApplicationLog>>> Handle(
        [FromServices] HexusConfigurationManager configuration,
        [FromServices] ProcessLogsService processLogsService,
        [FromServices] IHostApplicationLifetime hostLifetime,
        [FromRoute] string name,
        [FromQuery] DateTimeOffset? before = null,
        CancellationToken ct = default)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        // When the aspnet or the hostLifetime cancellation token get cancelled it cancels this as well
        var combinedCtSource = CancellationTokenSource.CreateLinkedTokenSource(ct, hostLifetime.ApplicationStopping);

        return new JsonArrayStreamResult<ApplicationLog>(processLogsService.GetLogs(application, before, combinedCtSource.Token));
    }
}

public class JsonArrayStreamResult<T>(IAsyncEnumerable<T> source) : IResult, IEndpointMetadataProvider
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);

        var jsonOptions = httpContext.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();

        var typeInfo = jsonOptions.Value.SerializerOptions.GetTypeInfo(typeof(T));

        await JsonSerializer.SerializeAsync(httpContext.Response.BodyWriter, source, typeInfo, httpContext.RequestAborted);
    }

    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        builder.Metadata.Add(new ProducesResponseTypeMetadata(
            statusCode: StatusCodes.Status200OK,
            type: typeof(IEnumerable<T>),
            contentTypes: ["application/json"]
        ));
    }
}
