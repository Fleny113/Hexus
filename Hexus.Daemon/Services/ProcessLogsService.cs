using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace Hexus.Daemon.Services;

internal partial class ProcessLogsService(ILogger<ProcessLogsService> logger)
{
    internal const string ApplicationStartedLog = "-- Application started --";
    internal static readonly CompositeFormat ApplicationStoppedLog = CompositeFormat.Parse("-- Application stopped [Exit code: {0}] --");

    private readonly Dictionary<string, List<Channel<ApplicationLog>>> _logChannels = [];

    internal void ProcessApplicationLog(HexusApplication application, LogType logType, string message)
    {
        if (logType != LogType.SYSTEM)
        {
            LogApplicationOutput(logger, application.Name, message);
        }

        var applicationLog = new ApplicationLog(DateTimeOffset.UtcNow, logType, message);

        if (_logChannels.TryGetValue(application.Name, out var channels))
        {
            channels.ForEach(channel => channel.Writer.TryWrite(applicationLog));
        }

        using var logFile = File.Open($"{EnvironmentHelper.ApplicationLogsDirectory}/{application.Name}.log", FileMode.Append, FileAccess.Write, FileShare.Read);
        using var log = new StreamWriter(logFile, Encoding.UTF8);

        log.WriteLine($"[{applicationLog.Date.DateTime:O},{applicationLog.LogType}] {applicationLog.Text}");
    }

    public async IAsyncEnumerable<ApplicationLog> GetLogs(HexusApplication application, DateTimeOffset? before, [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_logChannels.TryGetValue(application.Name, out var channels))
        {
            LogUnableToGetLogController(logger, application.Name);
            yield break;
        }

        var channel = Channel.CreateUnbounded<ApplicationLog>();
        channels.Add(channel);

        try
        {
            await foreach (var log in channel.Reader.ReadAllAsync(ct))
            {
                if (before.HasValue && log.Date > before) yield break;

                yield return log;
            }
        }
        finally
        {
            channel.Writer.Complete();
            channels.Remove(channel);
        }
    }

    public void RegisterApplication(HexusApplication application)
    {
        LogRegisteringApplication(logger, application.Name);
        _logChannels[application.Name] = [];
    }

    public bool UnregisterApplication(HexusApplication application)
    {
        LogUnregisteringApplication(logger, application.Name);
        return _logChannels.Remove(application.Name, out _);
    }

    public void DeleteApplication(HexusApplication application)
    {
        UnregisterApplication(application);
        File.Delete($"{EnvironmentHelper.ApplicationLogsDirectory}/{application.Name}.log");
    }


    [LoggerMessage(LogLevel.Warning, "Unable to get log channels for application \"{Name}\"")]
    private static partial void LogUnableToGetLogController(ILogger logger, string name);

    [LoggerMessage(LogLevel.Debug, "Application \"{Name}\" is being registered in the process logs service ")]
    private static partial void LogRegisteringApplication(ILogger logger, string name);

    [LoggerMessage(LogLevel.Debug, "Application \"{Name}\" is being unregistered in the process logs service ")]
    private static partial void LogUnregisteringApplication(ILogger logger, string name);

    [LoggerMessage(LogLevel.Trace, "Application \"{Name}\" says: '{OutputData}'")]
    private static partial void LogApplicationOutput(ILogger logger, string name, string outputData);
}
