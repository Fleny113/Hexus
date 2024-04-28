using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
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
        [FromQuery] int lines = 100,
        [FromQuery] bool noStreaming = false,
        [FromQuery] DateTimeOffset? before = null,
        [FromQuery] DateTimeOffset? after = null,
        CancellationToken ct = default)
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        // When the aspnet or the hexus cancellation token get cancelled it cancels this as well
        var combinedCtSource = CancellationTokenSource.CreateLinkedTokenSource(ct, HexusLifecycle.DaemonStoppingToken);

        // If the before is in the past we can disable steaming
        if (before is not null && before < DateTimeOffset.UtcNow) noStreaming = true;

        return TypedResults.Ok(GetLogs(application, logger, lines, noStreaming, before, after, combinedCtSource.Token));
    }

    private static async IAsyncEnumerable<ApplicationLog> GetLogs(
        HexusApplication application,
        ILogger<GetLogsEndpoint> logger,
        int lines,
        bool noStreaming,
        DateTimeOffset? before,
        DateTimeOffset? after,
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
            var logs = GetLogs(application, logger, lines, before, after);

            foreach (var log in logs.Reverse())
            {
                if (!IsLogDateInRange(log.Date, before, after)) continue;

                yield return log;
            }

            if (noStreaming || channel is null)
                yield break;

            await foreach (var log in channel.Reader.ReadAllAsync(ct))
            {
                if (!IsLogDateInRange(log.Date, before, after)) continue;

                yield return log;
            }
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

    private static IEnumerable<ApplicationLog> GetLogs(HexusApplication application, ILogger<GetLogsEndpoint> logger, int lines, DateTimeOffset? before = null, DateTimeOffset? after = null)
    {
        application.LogSemaphore.Wait();

        try
        {
            using var stream = File.OpenRead($"{EnvironmentHelper.LogsDirectory}/{application.Name}.log");
            using var reader = new StreamReader(stream, Encoding.UTF8);

            if (stream.Length <= 2)
                yield break;

            var lineFound = 0;

            // Go to the end of the file.
            stream.Position = stream.Length - 1;

            // If the last character is a LF we can skip it as it isn't a log line.
            if (stream.ReadByte() == '\n')
                stream.Position -= 2;

            while (lineFound < lines)
            {
                // We are in a line, so we go back until we find the start of this line.
                if (stream.Position != 0 && stream.ReadByte() != '\n')
                {
                    if (stream.Position >= 2)
                    {
                        stream.Position -= 2;
                        continue;
                    }

                    break;
                }

                var positionBeforeRead = stream.Position;

                reader.DiscardBufferedData();
                var line = reader.ReadLine();

                stream.Position = positionBeforeRead;

                if (string.IsNullOrEmpty(line))
                {
                    if (stream.Position >= 2)
                        stream.Position -= 2;

                    continue;
                }

                var logDateString = line[1..34];
                if (!TryLogTimeFormat(logDateString, out var logDate))
                {
                    LogFailedDateTimeParsing(logger, application.Name, logDateString);

                    if (stream.Position >= 2)
                    {
                        stream.Position -= 2;
                        continue;
                    }

                    break;
                }

                if (!IsLogDateInRange(logDate, before, after))
                {
                    if (stream.Position >= 2)
                    {
                        stream.Position -= 2;
                        continue;
                    }

                    break;
                }

                lineFound++;

                var logTypeString = line[35..41];
                var logText = line[43..];

                if (!LogType.TryParse(logTypeString.AsSpan(), out var logType))
                {
                    LogFailedTypeParsing(logger, application.Name, logTypeString);

                    if (stream.Position >= 2)
                    {
                        stream.Position -= 2;
                        continue;
                    }

                    break;
                }

                yield return new ApplicationLog(logDate, logType, logText);

                if (stream.Position >= 2)
                {
                    stream.Position -= 2;
                    continue;
                }

                break;
            }
        }
        finally
        {
            application.LogSemaphore.Release();
        }
    }

    private static bool TryLogTimeFormat(ReadOnlySpan<char> logDate, out DateTimeOffset dateTimeOffset)
    {
        return DateTimeOffset.TryParseExact(logDate, "O", null, DateTimeStyles.AssumeUniversal, out dateTimeOffset);
    }

    private static bool IsLogDateInRange(DateTimeOffset time, DateTimeOffset? before = null, DateTimeOffset? after = null)
    {
        if (before is not null && time > before.Value)
            return false;

        if (after is not null && time < after.Value)
            return false;

        return true;
    }

    [LoggerMessage(LogLevel.Warning, "There was an error parsing the log file for application {Name}: Couldn't parse \"{LogDate}\" as a DateTime. Skipping log line.")]
    private static partial void LogFailedDateTimeParsing(ILogger<GetLogsEndpoint> logger, string name, string logDate);

    [LoggerMessage(LogLevel.Warning, "There was an error parsing the log file for application {Name}: Couldn't parse \"{LogType}\" as a LogType. Skipping log line.")]
    private static partial void LogFailedTypeParsing(ILogger<GetLogsEndpoint> logger, string name, string logType);
}
