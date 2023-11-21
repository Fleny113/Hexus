using Hexus.Daemon;
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
        }
    };

    public static HttpClient HttpClient { get; } = new(HttpClientHandler)
    {
        BaseAddress = new Uri("http://hexus-socket")
    };

    public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolverChain =
        {
            AppJsonSerializerContext.Default
        }
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
}
