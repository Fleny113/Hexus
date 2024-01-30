using Hexus.Daemon;
using Hexus.Daemon.Contracts;
using Spectre.Console;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

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

    private static HttpClient HttpClient { get; } = new(HttpClientHandler)
    {
        BaseAddress = new Uri("http://hexus-socket"),
    };

    public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolverChain = { AppJsonSerializerContext.Default },
    };

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

    public static async Task<HttpResponseMessage> PostAsJsonAsync<T>(string status, [StringSyntax(StringSyntaxAttribute.Uri)] string url, T? content, JsonSerializerOptions jsonOptions, CancellationToken ct)
    {
        return await PrettyConsole.Out.Status()
            .Spinner(PrettyConsole.Spinner)
            .SpinnerStyle(PrettyConsole.SpinnerStyle)
            .StartAsync(status, _ => HttpClient.PostAsJsonAsync(url, content, jsonOptions, ct));
    }

    public static async Task<HttpResponseMessage> PatchAsync(string status, [StringSyntax(StringSyntaxAttribute.Uri)] string url, HttpContent? content, CancellationToken ct)
    {
        return await PrettyConsole.Out.Status()
            .Spinner(PrettyConsole.Spinner)
            .SpinnerStyle(PrettyConsole.SpinnerStyle)
            .StartAsync(status, _ => HttpClient.PatchAsync(url, content, ct));
    }

    public static async Task<HttpResponseMessage> PatchAsJsonAsync<T>(string status, [StringSyntax(StringSyntaxAttribute.Uri)] string url, T? content, JsonSerializerOptions jsonOptions, CancellationToken ct)
    {
        return await PrettyConsole.Out.Status()
            .Spinner(PrettyConsole.Spinner)
            .SpinnerStyle(PrettyConsole.SpinnerStyle)
            .StartAsync(status, _ => HttpClient.PatchAsJsonAsync(url, content, jsonOptions, ct));
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
        ErrorResponse? response;

        switch (request)
        {
            case { StatusCode: HttpStatusCode.BadRequest, Content.Headers.ContentType.MediaType: "application/problem+json" }:
            {
                var validationResponse = await request.Content.ReadFromJsonAsync<ProblemDetails>(JsonSerializerOptions, ct);

                Debug.Assert(validationResponse is not null);

                var errorString = string.Join("\n", validationResponse.Errors.SelectMany(kvp => kvp.Value.Select(v => $"- [tan]{kvp.Key}[/]: {v}")));

                response = new ErrorResponse($"Validation errors: \n{errorString}");
                break;
            }
            case { StatusCode: HttpStatusCode.NotFound }:
            {
                response = new ErrorResponse("No application with this name has been found.");
                break;
            }
            default:
            {
                try
                {
                    response = await request.Content.ReadFromJsonAsync<ErrorResponse>(JsonSerializerOptions, ct);
                    response ??= new ErrorResponse("The daemon had an internal server error.");
                    break;
                }
                catch
                {
                    response = new ErrorResponse("<Unable to get the error from the daemon>");
                    break;
                }
            }
        }

        PrettyConsole.Error.MarkupLine($"There [indianred1]was an error[/] in handling the request: {response.Error}");
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
