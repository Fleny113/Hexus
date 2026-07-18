using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Interop;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Windows.Win32.System.Console;

namespace Hexus.Daemon.Services;

internal partial class ProcessManagerService(ILoggerFactory loggerFactory, ProcessLogsService processLogsService, StateManagerService stateManagerService) : IConfigRelodable
{
    private readonly ILogger<ProcessManagerService> _logger = loggerFactory.CreateLogger<ProcessManagerService>();
    private readonly Dictionary<ApplicationConfiguration, ApplicationState> _applicationProcessStates = [];

    // If the application can't live for more then 30 seconds, after the 10 attempts to restart it, it will be considerate crashed
    private static readonly TimeSpan ResetTimeWindow = TimeSpan.FromSeconds(30);
    private const int MaxRestarts = 10;

    public SpawnProcessError? StartApplication(ApplicationConfiguration application)
    {
        if (IsApplicationProcessRunning(application, out _, out _))
            return null;

        var processInfo = new ProcessStartInfo
        {
            FileName = application.Executable,
            Arguments = application.Arguments,
            WorkingDirectory = application.WorkingDirectory,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            // We need to disable the UTF8 BOM or else applications will have a `EF BB BF` byte sequence at the start of the input and output
            StandardOutputEncoding = ProcessLogsService.Utf8EncodingWithoutBom,
            StandardErrorEncoding = ProcessLogsService.Utf8EncodingWithoutBom,
            StandardInputEncoding = ProcessLogsService.Utf8EncodingWithoutBom,
        };

        processInfo.Environment.Clear();

        foreach (var (key, value) in application.EnvironmentVariables)
            processInfo.Environment.Add(key, value);

        var (process, error) = SpawnProcess(processInfo);

        if (process is null)
            return error;

        var state = GetApplicationState(application);
        state.Process = process;
        state.Status = ApplicationStatus.Running;

        // Enable the emitting of events (like Exited)
        process.EnableRaisingEvents = true;

        processLogsService.ProcessApplicationLog(application, LogType.SYSTEM, ApplicationLog.ApplicationStartedLog);

        // Setup log handling 
        _ = HandleLogs(application, process, LogType.STDOUT);
        _ = HandleLogs(application, process, LogType.STDERR);

        // Register callbacks
        state.RestartCallback ??= (_, _) => _ = HandleProcessRestart(state);

        process.Exited += (_, _) => AcknowledgeProcessExit(state);
        process.Exited += state.RestartCallback;

        return null;
    }

    public bool StopApplication(ApplicationConfiguration application, bool forceStop = false)
    {
        if (!TryGetApplicationStateIfExists(application, out var state))
            return false;

        return StopApplication(state, forceStop);
    }

    private bool StopApplication(ApplicationState state, bool forceStop = false)
    {
        // If we aborted a restart we stopped the application
        if (AbortProcessRestart(state))
        {
            state.Status = ApplicationStatus.Stopped;
            return true;
        }

        if (!IsApplicationProcessRunning(state, out var process))
            return false;

        state.Status = ApplicationStatus.Stopping;

        // Remove the restart event handler, or else it will restart the process as soon as it stops
        process.Exited -= state.RestartCallback;

        // To be sure that the Exited event is called before we return, we use a ManualResetEvent to wait for the delegates to be called
        using var waitHandle = new ManualResetEvent(false);
        process.Exited += (_, _) => waitHandle.Set();

        StopProcess(process, forceStop);

        // Wait for the process Exited event to be called
        waitHandle.WaitOne();

        return true;
    }

    public void StopApplications()
    {
        Parallel.ForEach(_applicationProcessStates, t => StopApplication(t.Value));
    }

    internal ApplicationState GetApplicationState(ApplicationConfiguration application)
    {
        var state = _applicationProcessStates.GetOrCreate(application, app => new ApplicationState { Configuration = app });
        var persistantState = stateManagerService.LoadApplicationState(application);

        persistantState?.ApplyTo(state);

        return state;
    }

    internal bool TryGetApplicationStateIfExists(ApplicationConfiguration application, [NotNullWhen(true)] out ApplicationState? state)
    {
        return _applicationProcessStates.TryGetValue(application, out state);
    }

    internal bool IsApplicationProcessRunning(ApplicationConfiguration application, [NotNullWhen(true)] out ApplicationState? state,
        [NotNullWhen(true)] out Process? process)
    {
        process = null;
        return TryGetApplicationStateIfExists(application, out state) && IsApplicationProcessRunning(state, out process);
    }

    internal static bool IsApplicationProcessRunning(ApplicationState state, [NotNullWhen(true)] out Process? process)
    {
        process = state.Process;

        try
        {
            return state.Process is { HasExited: false };
        }
        catch (InvalidOperationException exception) when (exception.Message == "No process is associated with this object.")
        {
            process = state.Process = null;

            return false;
        }
    }

    public bool SendToApplication(ApplicationConfiguration application, ReadOnlySpan<char> text, bool newLine = true)
    {
        if (!IsApplicationProcessRunning(application, out _, out var process))
            return false;

        process.StandardInput.Write(text);
        if (newLine) process.StandardInput.WriteLine();

        return true;
    }

    /// <summary>
    /// This is a hard kill of the application and will not prevent restarts, use StopApplication to gracefully stop an application
    /// </summary>
    internal void KillApplication(ApplicationConfiguration application)
    {
        if (!IsApplicationProcessRunning(application, out _, out var process))
            return;

        try
        {
            process.Kill(true);
        }
        catch (InvalidOperationException exception) when (exception.Message == "No process is associated with this object.")
        {
            // We don't want to do anything. The application is already killed so nothing to do
        }
        catch (Exception exception)
        {
            LogFailedApplicationStop(_logger, exception);
        }
    }

    public ReloadResult ReloadConfiguration(ConfigurationDiff diff)
    {
        List<string> actions = [];
        List<string> errors = [];

        foreach (var app in diff.Removed)
        {
            StopApplication(app);
            stateManagerService.DeleteApplicationState(app);
            _applicationProcessStates.Remove(app);

            actions.Add($"Stopped removed application {app.Name}");
        }

        foreach (var app in diff.Added)
        {
            if (!app.Enabled) continue;

            StartApplication(app);

            actions.Add($"Starting added and enabled application {app.Name}");
        }

        foreach (var (old, update) in diff.Modified)
        {
            var shouldRestart = old.Executable != update.Executable ||
                                old.Arguments != update.Arguments ||
                                old.WorkingDirectory != update.WorkingDirectory ||
                                old.EnvironmentVariables != update.EnvironmentVariables;

            // Move the state to the new configuration
            if (_applicationProcessStates.Remove(old, out var state))
            {
                state.Configuration = update;
                _applicationProcessStates.Add(update, state);
            }

            if (!shouldRestart) continue;

            actions.Add($"Restarting modified application {update.Name} due to changes that require a restart");

            StopApplication(update);

            if (StartApplication(update) is { } error)
            {
                errors.Add($"Failed to start application {update.Name}: {error.MapToErrorString()}");
            }
        }

        return new ReloadResult(actions, [], errors);
    }

    private async Task HandleLogs(ApplicationConfiguration application, Process process, LogType logType)
    {
        var streamReader = logType switch
        {
            LogType.STDOUT => process.StandardOutput,
            LogType.STDERR => process.StandardError,
            _ => throw new ArgumentException("An invalid LogType was passed in", nameof(logType)),
        };

        while (true)
        {
            var str = await streamReader.ReadLineAsync();
            if (str is null) break;

            processLogsService.ProcessApplicationLog(application, logType, str);
        }
    }

    #region Start Process Internals

    private static (Process?, SpawnProcessError?) SpawnProcess(ProcessStartInfo startInfo)
    {
        try
        {
            var process = Process.Start(startInfo);

            return process is null or { HasExited: true }
                ? (null, SpawnProcessError.ExitEarly)
                : (process, null);
        }
        catch (Win32Exception exception)
        {
            // The first is the Linux error, the second is the Win32 error
            // If the executable is not found
            if (exception.Message.EndsWith("No such file or directory") || exception.Message.EndsWith("The system cannot find the file specified."))
            {
                return (null, SpawnProcessError.NotFound);
            }

            // If the executable can not be accessed
            if (exception.Message.EndsWith("Permission denied") || exception.Message.EndsWith("Access is denied."))
            {
                return (null, SpawnProcessError.PermissionDenied);
            }

            // If the executable is invalid
            if (exception.Message.EndsWith("Exec format error") ||
                exception.Message.EndsWith("The specified executable is not a valid application for this OS platform."))
            {
                return (null, SpawnProcessError.InvalidExecutable);
            }

            // If the command is too long, the first is the Linux error for the arguments, the second one is the Linux error for the file, the third one is the Win32 error
            if (exception.Message.EndsWith("Argument list too long") || exception.Message.EndsWith("File name too long") ||
                exception.Message.EndsWith("The filename or extension is too long."))
            {
                return (null, SpawnProcessError.CommandTooLong);
            }

            return (null, SpawnProcessError.Unknown);
        }
    }

    internal enum SpawnProcessError
    {
        ExitEarly,
        NotFound,
        PermissionDenied,
        InvalidExecutable,
        CommandTooLong,
        Unknown,
    }

    #endregion

    #region Stop Process Internals

    private void StopProcess(Process process, bool forceStop)
    {
        if (forceStop)
        {
            process.Kill(true);
            return;
        }

        // SendSignal can send -1 if the UNIX kill call returns an error or if the windows interop errors out at any point
        var code = ProcessSignals.SendSignal(process.Id, WindowsCtrlType.CtrlC, UnixSignal.SigInt);

        try
        {
            // If in 30 seconds the process doesn't get killed (it has handled the SIGINT signal and not exited) then force stop it
            if (code is 0 && process.WaitForExit(TimeSpan.FromSeconds(30)))
                return;

            process.Kill(true);
        }
        catch (InvalidOperationException exception) when (exception.Message == "No process is associated with this object.")
        {
            // We don't want to do anything. The application is already killed so nothing to do
        }
        catch (Exception exception)
        {
            LogFailedApplicationStop(_logger, exception);

            // If it has already exited there is no point in sending another kill
            if (process.HasExited)
                return;

            // Fallback to the .NET build-in Kernel call to force stop the process
            process.Kill(true);
        }
    }

    #endregion

    #region Exit process Internals

    private static bool AbortProcessRestart(ApplicationState state)
    {
        // Abort the restart if there are any, then delete the tokens
        state.AbortRestartCancellationTokenSource?.Cancel();
        state.AbortRestartCancellationTokenSource = null;

        return state.Status is ApplicationStatus.Restarting;
    }

    private void AcknowledgeProcessExit(ApplicationState state)
    {
        if (state.Process is null)
            return;

        var exitCode = state.Process.ExitCode;

        processLogsService.ProcessApplicationLog(state.Configuration, LogType.SYSTEM, string.Format(null, ApplicationLog.ApplicationStoppedLog, exitCode));

        state.Status = ApplicationStatus.Stopped;
        state.Process.Close();
        state.Process = null;

        LogAcknowledgeProcessExit(_logger, state.Configuration.Name, exitCode);
    }

    private async Task HandleProcessRestart(ApplicationState state)
    {
        state.RestartCount++;

        state.ClearRestartsTimer ??= new Timer(s => ClearConsequentialRestarts((ApplicationState)s!), state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        if (state.RestartCount > MaxRestarts)
        {
            LogCrashedApplication(_logger, state.Configuration.Name, state.RestartCount, ResetTimeWindow);

            state.ClearRestartsTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            state.Status = ApplicationStatus.Crashed;

            stateManagerService.SaveApplicationState(state.Configuration, StateManagerService.PersistantApplicationState.From(state));

            return;
        }

        state.Status = ApplicationStatus.Restarting;

        state.AbortRestartCancellationTokenSource = new CancellationTokenSource();

        // Debounce the reset of the restart count
        state.ClearRestartsTimer.Change(ResetTimeWindow, Timeout.InfiniteTimeSpan);

        var delay = CalculateDelay(state.RestartCount);
        LogRestartAttemptDelay(_logger, state.Configuration.Name, delay.TotalSeconds);

        try
        {
            await Task.Delay(delay, state.AbortRestartCancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // The restart was aborted, we don't want to restart the application
            return;
        }

        state.AbortRestartCancellationTokenSource = null;

        StartApplication(state.Configuration);
    }

    private void ClearConsequentialRestarts(ApplicationState state)
    {
        LogConsequentialRestartsStop(_logger, state.RestartCount, state.Configuration.Name);

        state.RestartCount = 0;
    }

    private static TimeSpan CalculateDelay(int restart) =>
        restart switch
        {
            1 => TimeSpan.FromSeconds(.1),
            2 or 3 => TimeSpan.FromSeconds(.5),
            4 or 5 => TimeSpan.FromSeconds(1),
            6 or 7 => TimeSpan.FromSeconds(2),
            8 or 9 => TimeSpan.FromSeconds(4),
            10 => TimeSpan.FromSeconds(8),
            _ => throw new ArgumentOutOfRangeException(nameof(restart)),
        };

    #endregion

    internal record ApplicationState
    {
        public required ApplicationConfiguration Configuration { get; set; }

        public Process? Process { get; set; }
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Stopped;

        // Restart data
        public int RestartCount { get; set; }
        public Timer? ClearRestartsTimer { get; set; }

        public CancellationTokenSource? AbortRestartCancellationTokenSource { get; set; }

        // We have to cache this delegate so we can remove it in StopApplication
        public EventHandler? RestartCallback { get; set; }
    }

    [LoggerMessage(LogLevel.Warning, "Application \"{Name}\" has exited for {MaxRestarts} times in the time window ({TimeWindow}). It will be considered crashed.")]
    private static partial void LogCrashedApplication(ILogger logger, string name, int maxRestarts, TimeSpan timeWindow);

    [LoggerMessage(LogLevel.Debug, "Acknowledging about \"{Name}\" exiting with code: {ExitCode}")]
    private static partial void LogAcknowledgeProcessExit(ILogger logger, string name, int exitCode);

    [LoggerMessage(LogLevel.Debug, "After {Restarts} restarts, application \"{Name}\" stopped restarting.")]
    private static partial void LogConsequentialRestartsStop(ILogger logger, int restarts, string name);

    [LoggerMessage(LogLevel.Debug, "Attempting to restart application \"{Name}\", waiting for {Seconds} seconds before restarting.")]
    private static partial void LogRestartAttemptDelay(ILogger logger, string name, double seconds);

    [LoggerMessage(LogLevel.Debug, "Unable to stop process!")]
    private static partial void LogFailedApplicationStop(ILogger logger, Exception exception);
}
