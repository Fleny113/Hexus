using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using System.Text.Json.Serialization;
using Tomlyn;
using Tomlyn.Serialization;

namespace Hexus.Daemon.Services;

internal partial class StateManagerService(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<StateManagerService>();

    public void SaveApplicationState(ApplicationConfiguration application, PersistantApplicationState state)
    {
        LogSaving(_logger, application.Name, state);

        var path = GetApplicationStatePath(application.Name);

        File.WriteAllText(path, TomlSerializer.Serialize(state, StateSerializerContext.Default.PersistantApplicationState));
    }

    public PersistantApplicationState? LoadApplicationState(ApplicationConfiguration application)
    {
        var path = GetApplicationStatePath(application.Name);

        if (!File.Exists(path))
        {
            return null;
        }

        LogLoading(_logger, application.Name);

        using var file = File.OpenRead(path);

        if (!TomlSerializer.TryDeserialize<PersistantApplicationState>(file, StateSerializerContext.Default, out var state))
        {
            LogErrorLoading(_logger, application.Name, new Exception("Failed to deserialize state file."));
            return null;
        }

        return state;
    }

    public void DeleteApplicationState(ApplicationConfiguration application)
    {
        var path = GetApplicationStatePath(application.Name);

        if (!File.Exists(path)) return;

        LogDeleting(_logger, application.Name);

        File.Delete(path);
    }

    private static string GetApplicationStatePath(string name) => Path.Combine(HexusPaths.ApplicationStatesDirectory, $"{name}.state");

    internal sealed record PersistantApplicationState
    {
        public bool Crashed { get; init; }

        internal static PersistantApplicationState From(ProcessManagerService.ApplicationState state)
        {
            return new PersistantApplicationState { Crashed = state.Status == ApplicationStatus.Crashed, };
        }

        internal void ApplyTo(ProcessManagerService.ApplicationState state)
        {
            if (Crashed) state.Status = ApplicationStatus.Crashed;
        }
    }

    [LoggerMessage(LogLevel.Debug, "Loading application \"{Name}\", state.")]
    private static partial void LogLoading(ILogger logger, string name);

    [LoggerMessage(LogLevel.Error, "Error loading application \"{Name}\", state.")]
    private static partial void LogErrorLoading(ILogger logger, string name, Exception exception);

    [LoggerMessage(LogLevel.Debug, "Saving application \"{Name}\", state: {State}.")]
    private static partial void LogSaving(ILogger logger, string name, PersistantApplicationState state);

    [LoggerMessage(LogLevel.Debug, "Deleting application \"{name}\"")]
    private static partial void LogDeleting(ILogger logger, string name);
}

[TomlSerializable(typeof(StateManagerService.PersistantApplicationState))]
[TomlSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.KebabCaseLower)]
internal partial class StateSerializerContext : TomlSerializerContext;
