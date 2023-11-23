using Hexus.Daemon;
using Hexus.Daemon.Contracts;
using Spectre.Console;
using System.Diagnostics;
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

    public static HttpClient HttpClient { get; } = new(HttpClientHandler)
    {
        BaseAddress = new Uri("http://hexus-socket"),
    };

    public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolverChain =
        {
            AppJsonSerializerContext.Default,
        },
    };

    public static async ValueTask<bool> CheckForRunningDaemon(CancellationToken ct)
    {
        if (!File.Exists(Configuration.HexusConfiguration.UnixSocket))
            return false;

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
}
