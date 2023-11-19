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

}
