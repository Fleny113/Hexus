using Hexus.Daemon;
using Hexus.Daemon.Contracts.Responses;
using Microsoft.AspNetCore.Http;
using Spectre.Console;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Hexus;

internal static class HttpInvocation
{
    private static readonly HttpMessageHandler HttpClientHandler = new SocketsHttpHandler
    {
        ConnectCallback = async (_, ct) =>
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            var endpoint = new UnixDomainSocketEndPoint(Configuration.HexusConfiguration.UnixSocket);

            await socket.ConnectAsync(endpoint, ct);

            return new NetworkStream(socket, ownsSocket: true);
        },
    };

    public static HttpClient HttpClient { get; } = new(HttpClientHandler)
    {
        BaseAddress = new Uri("http://hexus-socket"),
    };

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolverChain = { AppJsonSerializerContext.Default },
    };

    public static AppJsonSerializerContext JsonSerializerContext { get; } = new(JsonSerializerOptions);

    #region Status wrappers

    public static async Task<bool> CheckForRunningDaemon(CancellationToken ct)
    {
        if (!File.Exists(Configuration.HexusConfiguration.UnixSocket))
            return false;

        return await PrettyConsole.Out.Status()
            .Spinner(PrettyConsole.Spinner)
            .SpinnerStyle(PrettyConsole.SpinnerStyle)
            .StartAsync("Checking daemon status", _ => CheckForRunningDaemonCore(ct));
    }

    public static async Task<HttpResponseMessage> GetAsync(string status, [StringSyntax(StringSyntaxAttribute.Uri)] string url, CancellationToken ct)
    {
        return await PrettyConsole.Out.Status()
            .Spinner(PrettyConsole.Spinner)
            .SpinnerStyle(PrettyConsole.SpinnerStyle)
            .StartAsync(status, _ => HttpClient.GetAsync(url, ct));
    }

    public static async Task<HttpResponseMessage> GetAsync(string status, [StringSyntax(StringSyntaxAttribute.Uri)] string url, HttpCompletionOption completionOption, CancellationToken ct)
    {
        return await PrettyConsole.Out.Status()
            .Spinner(PrettyConsole.Spinner)
            .SpinnerStyle(PrettyConsole.SpinnerStyle)
            .StartAsync(status, _ => HttpClient.GetAsync(url, completionOption, ct));
    }

    public static async Task<HttpResponseMessage> PostAsync(string status, [StringSyntax(StringSyntaxAttribute.Uri)] string url, HttpContent? content, CancellationToken ct)
    {
        return await PrettyConsole.Out.Status()
            .Spinner(PrettyConsole.Spinner)
            .SpinnerStyle(PrettyConsole.SpinnerStyle)
            .StartAsync(status, _ => HttpClient.PostAsync(url, content, ct));
    }

    public static async Task<HttpResponseMessage> PostAsJsonAsync<T>(string status, [StringSyntax(StringSyntaxAttribute.Uri)] string url, T? content, JsonSerializerContext context, CancellationToken ct)
    {
        var typeInfo = context.GetTypeInfo(typeof(T));

        if (typeInfo is not JsonTypeInfo<T?> jsonTypeInfo)
        {
            throw new ArgumentException("The provided context is not of the correct type.", nameof(context));
        }

        return await PrettyConsole.Out.Status()
            .Spinner(PrettyConsole.Spinner)
            .SpinnerStyle(PrettyConsole.SpinnerStyle)
            .StartAsync(status, _ => HttpClient.PostAsJsonAsync(url, content, jsonTypeInfo, ct));
    }

    public static async Task<HttpResponseMessage> PatchAsync(string status, [StringSyntax(StringSyntaxAttribute.Uri)] string url, HttpContent? content, CancellationToken ct)
    {
        return await PrettyConsole.Out.Status()
            .Spinner(PrettyConsole.Spinner)
            .SpinnerStyle(PrettyConsole.SpinnerStyle)
            .StartAsync(status, _ => HttpClient.PatchAsync(url, content, ct));
    }

    public static async Task<HttpResponseMessage> PatchAsJsonAsync<T>(string status, [StringSyntax(StringSyntaxAttribute.Uri)] string url, T? content, JsonSerializerContext context, CancellationToken ct)
    {
        var typeInfo = context.GetTypeInfo(typeof(T));

        if (typeInfo is not JsonTypeInfo<T?> jsonTypeInfo)
        {
            throw new ArgumentException("The provided context is not of the correct type.", nameof(context));
        }

        return await PrettyConsole.Out.Status()
            .Spinner(PrettyConsole.Spinner)
            .SpinnerStyle(PrettyConsole.SpinnerStyle)
            .StartAsync(status, _ => HttpClient.PatchAsJsonAsync(url, content, jsonTypeInfo, ct));
    }

    public static async Task<HttpResponseMessage> DeleteAsync(string status, [StringSyntax(StringSyntaxAttribute.Uri)] string url, CancellationToken ct)
    {
        return await PrettyConsole.Out.Status()
            .Spinner(PrettyConsole.Spinner)
            .SpinnerStyle(PrettyConsole.SpinnerStyle)
            .StartAsync(status, _ => HttpClient.DeleteAsync(url, ct));
    }

    #endregion

    public static async Task HandleFailedHttpRequestLogging(HttpResponseMessage request, CancellationToken ct)
    {
        string response;

        switch (request)
        {
            case { StatusCode: HttpStatusCode.BadRequest, Content.Headers.ContentType.MediaType: "application/problem+json" }:
                {
                    var problemDetails = await request.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonSerializerContext.HttpValidationProblemDetails, ct);

                    Debug.Assert(problemDetails is not null);

                    var problems = problemDetails.Errors.SelectMany(kvp => kvp.Value.Select(v => $"- [tan]{kvp.Key}[/]: {v}"));
                    response = $"Validation errors: \n{string.Join("\n", problems)}";
                    break;
                }
            case { StatusCode: HttpStatusCode.BadRequest }:
                {
                    var error = await request.Content.ReadFromJsonAsync<GenericFailureResponse>(JsonSerializerContext.GenericFailureResponse, ct);

                    Debug.Assert(error is not null);

                    response = error.Message;
                    break;
                }
            case { StatusCode: HttpStatusCode.NotFound }:
                {
                    response = "No application with this name has been found.";
                    break;
                }
            case { StatusCode: >= HttpStatusCode.InternalServerError }:
                {
                    response = "The daemon had an internal server error.";
                    break;
                }
            default:
                {
                    response = $"""
                        Unknown error,
                        HTTP status code: {request.StatusCode}
                        Content-Type: {request.Content.Headers.ContentType?.MediaType}
                        Body: {await request.Content.ReadAsStringAsync(ct)}
                        """;
                    break;
                }
        }

        PrettyConsole.Error.MarkupLine($"There [indianred1]was an error[/] in handling the request: {response}");
    }

    private static async Task<bool> CheckForRunningDaemonCore(CancellationToken ct)
    {
        try
        {
            // This in fact returns a 404, we only care to check if the daemon is running, so this is fine.
            await HttpClient.GetAsync("/", ct);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
