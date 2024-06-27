using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Interop;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Hexus.Daemon.Services;

internal partial class ProcessManagerService(
    ILogger<ProcessManagerService> logger,
    HexusConfigurationManager configManager,
    ProcessLogsService processLogsService)
{
    private readonly Dictionary<Process, HexusApplication> _processToApplicationMap = [];
    private readonly Dictionary<string, Process> _applicationToProcessMap = [];

    // We need to disable the UTF8 identifier or else applications will have a `EF BB BF` character in their stdin
    private static readonly UTF8Encoding _processEncoding = new(encoderShouldEmitUTF8Identifier: false);

    public bool StartApplication(HexusApplication application)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = application.Executable,
            Arguments = application.Arguments,
            WorkingDirectory = application.WorkingDirectory,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = _processEncoding,
            StandardErrorEncoding = _processEncoding,
            StandardInputEncoding = _processEncoding,
        };

        processInfo.Environment.Clear();

        foreach (var (key, value) in application.EnvironmentVariables)
            processInfo.Environment.Add(key, value);

        var process = Process.Start(processInfo);

        if (process is null or { HasExited: true })
            return false;

        _processToApplicationMap[process] = application;
        _applicationToProcessMap[application.Name] = process;

        // Enable the emitting of events (like Exited)
        process.EnableRaisingEvents = true;

        processLogsService.ProcessApplicationLog(application, LogType.SYSTEM, ProcessLogsService.ApplicationStartedLog);

        // Setup log handling 
        _ = HandleLogs(application, process, LogType.STDOUT);
        _ = HandleLogs(application, process, LogType.STDERR);

        // Register callbacks
        process.Exited += AcknowledgeProcessExit;
        process.Exited += HandleProcessRestart;

        application.Status = HexusApplicationStatus.Running;
        configManager.SaveConfiguration();

        return true;
    }

    public bool StopApplication(HexusApplication application, bool forceStop = false)
    {
        if (!IsApplicationRunning(application, out var process))
            return false;

        // Remove the restart event handler, or else it will restart the process as soon as it stops
        process.Exited -= HandleProcessRestart;
        process.Exited += ClearApplicationStateOnExit;

        StopProcess(process, forceStop);

        application.Status = HexusApplicationStatus.Exited;

        _processToApplicationMap.Remove(process, out _);
        _applicationToProcessMap.Remove(application.Name, out _);

        // If the daemon is shutting down we don't want to save, or else when the daemon is booted up again, all the applications will be marked as stopped
        if (!HexusLifecycle.IsDaemonStopped)
            configManager.SaveConfiguration();

        return true;
    }

    public void StopApplications()
    {
        Parallel.ForEach(_processToApplicationMap, tuple => StopApplication(tuple.Value));
    }

    public bool IsApplicationRunning(HexusApplication application, [NotNullWhen(true)] out Process? process)
    {
        if (!_applicationToProcessMap.TryGetValue(application.Name, out process))
        {
            return false;
        }

        try
        {
            return application is { Status: HexusApplicationStatus.Running } && process is { HasExited: false };
        }
        catch (InvalidOperationException exception) when (exception.Message == "No process is associated with this object.")
        {
            // The process does not exist. so it isn't running
            _applicationToProcessMap.Remove(application.Name, out _);
            _processToApplicationMap.Remove(process, out _);

            return false;
        }
    }

    public bool SendToApplication(HexusApplication application, ReadOnlySpan<char> text, bool newLine = true)
    {
        if (!IsApplicationRunning(application, out var process))
            return false;

        if (newLine)
            process.StandardInput.WriteLine(text);
        else
            process.StandardInput.Write(text);

        return true;
    }

    #region Stop Process Internals

    private void StopProcess(Process process, bool forceStop)
    {
        if (forceStop)
        {
            KillProcess(process, true);
            return;
        }

        // NativeSendSignal can send -1 if the UNIX kill call returns an error or if the windows interop errors out at any point
        var code = ProcessSignals.NativeSendSignal(process.Id, WindowsCtrlType.CtrlC, UnixSignal.SigInt);

        try
        {
            // If in 30 seconds the process doesn't get killed (it has handled the SIGINT signal and not exited) then force stop it
            if (code is 0 && process.WaitForExit(TimeSpan.FromSeconds(30)))
                return;

            KillProcess(process, true);
        }
        catch (InvalidOperationException exception) when (exception.Message == "No process is associated with this object.")
        {
            // We don't want to do anything. The application is already killed so nothing to do
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Error during the stop of a process");

            // If it has already exited there is no point in sending another kill
            if (process.HasExited)
                return;

            // Fallback to the .NET build-in Kernel call to force stop the process
            KillProcess(process, true);
        }
    }

    private static void KillProcess(Process process, bool killTree = false)
    {
        process.Kill(killTree);
        // The getter for HasExited calls the Exited event if it hasn't been called yet
        _ = process.HasExited;
    }

    #endregion

    #region Log process events handlers

    private async Task HandleLogs(HexusApplication application, Process process, LogType logType)
    {
        var streamReader = logType switch
        {
            LogType.STDOUT => process.StandardOutput,
            LogType.STDERR => process.StandardError,
            _ => throw new ArgumentException("An invalid LogType was passed in", nameof(logType)),
        };

        while (!process.HasExited)
        {
            var str = await streamReader.ReadLineAsync();
            if (str is null) continue;

            processLogsService.ProcessApplicationLog(application, logType, str);
        }
    }

    #endregion

    #region Exit process event handlers

    // If the application can't live for more then 30 seconds, after the 10 attempts to restart it, it will be considerate crashed
    private static readonly TimeSpan ResetTimeWindow = TimeSpan.FromSeconds(30);
    private const int MaxRestarts = 10;

    private readonly Dictionary<string, (int Restarts, CancellationTokenSource? CancellationTokenSource)> _consequentialRestarts = [];

    private void AcknowledgeProcessExit(object? sender, EventArgs e)
    {
        if (sender is not Process process || !_processToApplicationMap.TryGetValue(process, out var application))
            return;

        var exitCode = process.ExitCode;

        processLogsService.ProcessApplicationLog(application, LogType.SYSTEM, string.Format(null, ProcessLogsService.ApplicationStoppedLog, exitCode));

        process.Close();

        LogAcknowledgeProcessExit(logger, application.Name, exitCode);
    }

    private void ClearApplicationStateOnExit(object? sender, EventArgs e)
    {
        if (sender is not Process process || !_processToApplicationMap.TryGetValue(process, out var application))
            return;

        _processToApplicationMap.Remove(process, out _);
        _applicationToProcessMap.Remove(application.Name, out _);
    }

    private void HandleProcessRestart(object? sender, EventArgs e)
    {
        if (sender is not Process process || !_processToApplicationMap.TryGetValue(process, out var application))
            return;

        var status = _consequentialRestarts.GetValueOrDefault(application.Name, (0, null));

        status.Restarts++;
        status.CancellationTokenSource?.Dispose();
        status.CancellationTokenSource = new CancellationTokenSource(ResetTimeWindow);

        _consequentialRestarts[application.Name] = status;
        _processToApplicationMap.Remove(process, out _);
        _applicationToProcessMap.Remove(application.Name, out _);

        if (status.Restarts > MaxRestarts)
        {
            LogCrashedApplication(logger, application.Name, status.Restarts, ResetTimeWindow.TotalSeconds);

            status.CancellationTokenSource.Dispose();
            _consequentialRestarts.Remove(application.Name, out _);

            application.Status = HexusApplicationStatus.Crashed;
            configManager.SaveConfiguration();

            return;
        }

        var delay = CalculateDelay(status.Restarts);
        status.CancellationTokenSource.Token.Register(ResetConsequentialRestarts, application.Name);

        LogRestartAttemptDelay(logger, application.Name, delay.TotalSeconds);

        Task.Delay(delay).ContinueWith(_ =>
        {
            StartApplication(application);
            configManager.SaveConfiguration();
        });
    }

    private void ResetConsequentialRestarts(object? state)
    {
        if (state is not string name)
            return;

        _consequentialRestarts.Remove(name, out var status);
        status.CancellationTokenSource?.Dispose();

        LogConsequentialRestartsStop(logger, status.Restarts, name);
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

    [LoggerMessage(LogLevel.Warning, "Application \"{Name}\" has exited for {MaxRestarts} times in the time window ({TimeWindow} seconds). It will be considered crashed")]
    private static partial void LogCrashedApplication(ILogger logger, string name, int maxRestarts, double timeWindow);

    [LoggerMessage(LogLevel.Debug, "Acknowledging about \"{Name}\" exiting with code: {ExitCode}")]
    private static partial void LogAcknowledgeProcessExit(ILogger logger, string name, int exitCode);

    [LoggerMessage(LogLevel.Debug, "After {Restarts} restarts, application \"{Name}\" stopped restarting")]
    private static partial void LogConsequentialRestartsStop(ILogger logger, int restarts, string name);

    [LoggerMessage(LogLevel.Debug, "Attempting to restart application \"{Name}\", waiting for {Seconds} seconds before restarting")]
    private static partial void LogRestartAttemptDelay(ILogger logger, string name, double seconds);
}
