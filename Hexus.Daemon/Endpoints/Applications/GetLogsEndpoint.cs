using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace Hexus.Daemon.Endpoints.Applications;

internal partial class GetLogsEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Get, "/{name}/logs")]
    public static Results<Ok<IAsyncEnumerable<ApplicationLog>>, NotFound> Handle(
        [FromServices] HexusConfiguration configuration,
        [FromServices] ILogger<GetLogsEndpoint> logger,
        [FromRoute] string name,
        [FromQuery] int lines = 10,
        [FromQuery] bool noStreaming = false,
        CancellationToken ct = default)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        // When the aspnet or hexus CTS get cancelled it cancels this as well
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, HexusLifecycle.DaemonStoppingToken);

        return TypedResults.Ok(GetLogs(application, logger, lines, noStreaming, combinedCts.Token));
    }

    private static async IAsyncEnumerable<ApplicationLog> GetLogs(
        HexusApplication application,
        ILogger<GetLogsEndpoint> logger,
        int lines,
        bool noStreaming,
        [EnumeratorCancellation] CancellationToken ct)
    {
        Channel<ApplicationLog>? channel = null;
        
        if (!noStreaming)
        {
            channel = Channel.CreateUnbounded<ApplicationLog>();
            application.LogChannels.Add(channel);
        }

        try
        {
            foreach (var log in GetLogs(application, logger, lines))
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

    private static IEnumerable<ApplicationLog> GetLogs(HexusApplication application, ILogger<GetLogsEndpoint> logger, int lines)
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

                var logDateSpan = line[1..21];
                var logTypeSpan = line[22..28];
                var logText = line[30..];

                if (TryLogTimeFormat(logDateSpan, out var logDate))
                {
                    LogFailedDateTimeParsing(logger, application.Name, logDateSpan);
                    continue;
                }

                if (!LogType.TryParse(logTypeSpan, out var logType))
                {
                    LogFailedTypeParsing(logger, application.Name, logTypeSpan);
                    continue;
                }
                
                yield return new ApplicationLog(logDate, logType, logText);
            }
        }
        finally
        {
            application.LogSemaphore.Release();
        }
    }

    private static bool TryLogTimeFormat(ReadOnlySpan<char> logDate, out DateTimeOffset dateTimeOffset)
    {
        return !DateTimeOffset.TryParseExact(logDate, ApplicationLog.DateTimeFormat, null, DateTimeStyles.AssumeUniversal, out dateTimeOffset);
    }

    [LoggerMessage(LogLevel.Warning, "There was an error parsing the log file for application {Name}: Couldn't parse {LogDate} as a DateTime. Skipping log line.")]
    private static partial void LogFailedDateTimeParsing(ILogger<GetLogsEndpoint> logger, string name, string logDate);
    
    [LoggerMessage(LogLevel.Warning, "There was an error parsing the log file for application {Name}: Couldn't parse {LogType} as a LogType. Skipping log line.")]
    private static partial void LogFailedTypeParsing(ILogger<GetLogsEndpoint> logger, string name, string logType);
}
