using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace Hexus.Daemon.Endpoints.Applications;

internal class GetLogsEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Get, "/{name}/logs")]
    public static Results<Ok<IAsyncEnumerable<string>>, NotFound> Handle(
        [FromServices] HexusConfiguration configuration,
        [FromRoute] string name,
        [FromQuery] int lines = 10,
        [FromQuery] bool noStreaming = false,
        CancellationToken ct = default)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        // When the aspnet or hexus CTS get cancelled it cancels this as well
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, HexusLifecycle.DaemonStoppingToken);

        return TypedResults.Ok(GetLogs(application, lines, noStreaming, combinedCts.Token));
    }

    private static async IAsyncEnumerable<string> GetLogs(
        HexusApplication application,
        int lines,
        bool noStreaming,
        [EnumeratorCancellation] CancellationToken ct)
    {
        Channel<string>? channel = null;
        
        if (!noStreaming)
        {
            channel = Channel.CreateUnbounded<string>();
            application.LogChannels.Add(channel);
        }

        try
        {
            foreach (var log in GetLogs(application, lines))
                yield return log;

            if (noStreaming || channel is null)
                yield break;

            await foreach (var log in channel.Reader.ReadAllAsync(ct))
                yield return log;
        }
        finally
        {
            if (channel is not null)
            {
                channel.Writer.Complete();
                application.LogChannels.Remove(channel);
            }
        }
    }

    private static IEnumerable<string> GetLogs(HexusApplication application, int lines)
    {
        application.LogSemaphore.Wait();

        try
        {
            using var stream = File.OpenRead($"{EnvironmentHelper.LogsDirectory}/{application.Name}.log");
            var newLineFound = 0;

            if (stream.Length == 0)
                yield break;

            // Go to the end of the file
            stream.Position = stream.Seek(-1, SeekOrigin.End);

            while (newLineFound <= lines)
            {
                if (stream.ReadByte() != '\n')
                {
                    // We are at the start of the stream, we can not go further behind
                    if (stream.Position <= 2)
                    {
                        stream.Position = 0;
                        break;
                    }

                    // Go back 2 characters, one for the consumed one by ReadByte and one to go actually back and don't re-read the same char each time
                    stream.Seek(-2, SeekOrigin.Current);
                    continue;
                }

                newLineFound++;

                // On the last iteration we don't want to move the position
                if (newLineFound <= lines && stream.Position >= 2)
                    stream.Seek(-2, SeekOrigin.Current);
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);

            for (var i = 0; i < lines; i++)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                yield return line;
            }
        }
        finally
        {
            application.LogSemaphore.Release();
        }
    }
}
