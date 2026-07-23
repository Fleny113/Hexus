using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Extensions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace Hexus.Daemon.Services;

public partial class ProcessLogsService(ILogger<ProcessLogsService> logger)
{
    internal static readonly UTF8Encoding Utf8EncodingWithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Dictionary<ApplicationConfiguration, List<Channel<ApplicationLog>>> _logChannels = [];

    public async IAsyncEnumerable<ApplicationLog> GetLogs(ApplicationConfiguration application, DateTimeOffset? before, [EnumeratorCancellation] CancellationToken ct)
    {
        var channels = _logChannels.GetOrCreate(application, _ => []);
        var channel = Channel.CreateUnbounded<ApplicationLog>();
        channels.Add(channel);

        try
        {
            await foreach (var log in channel.Reader.ReadAllAsync(ct))
            {
                if (before.HasValue && log.Date > before.Value) yield break;

                yield return log;
            }
        }
        finally
        {
            channel.Writer.Complete();
            channels.Remove(channel);

            if (channels.Count == 0) _logChannels.Remove(application);
        }
    }

    internal void ProcessApplicationLog(ApplicationConfiguration application, LogType logType, string message)
    {
        lock (application)
        {
            if (logType is not LogType.SYSTEM)
            {
                LogApplicationOutput(logger, application.Name, message);
            }

            var applicationLog = new ApplicationLog(DateTimeOffset.UtcNow, logType, message);

            if (_logChannels.TryGetValue(application, out var channels))
            {
                channels.ForEach(channel => channel.Writer.TryWrite(applicationLog));
            }

            using var logFile = File.Open($"{EnvironmentHelper.ApplicationLogsDirectory}/{application.Name}.log", FileMode.Append, FileAccess.Write, FileShare.Read);
            using var log = new StreamWriter(logFile, Utf8EncodingWithoutBom);

            log.Write($"[{applicationLog.Date.DateTime:O},{applicationLog.LogType}] {applicationLog.Text}\n");
        }
    }

    [LoggerMessage(LogLevel.Trace, "Application \"{Name}\" says: '{OutputData}'")]
    private static partial void LogApplicationOutput(ILogger logger, string name, string outputData);
}
