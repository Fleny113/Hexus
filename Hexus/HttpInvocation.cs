using System.Net.Sockets;

namespace Hexus;

internal static class HttpInvocation
{
    private static readonly HttpMessageHandler httpClientHandler = new SocketsHttpHandler
    {
        ConnectCallback = async (_, ct) =>
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            var endpoint = new UnixDomainSocketEndPoint(Configuration.HexusConfiguration.UnixSocket);

            await socket.ConnectAsync(endpoint, ct);

            return new NetworkStream(socket, ownsSocket: true);
        }
    };

    public static HttpClient HttpClient { get; } = new(httpClientHandler)
    {
        BaseAddress = new Uri("http://hexus-socket")
    };


    public static async ValueTask<bool> CheckForRunningDaemon()
    {
        if (!File.Exists(Configuration.HexusConfiguration.UnixSocket))
            return false;

        try
        {
            // This in fact returns a 404, we only care to check if the daemon is running, so this is fine.
            await HttpClient.GetAsync("/");

            return true;
        }
        catch
        {
            return false;
        }
    }
}
