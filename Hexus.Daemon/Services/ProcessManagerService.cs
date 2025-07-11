﻿using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Interop;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Windows.Win32.System.Console;

namespace Hexus.Daemon.Services;

internal partial class ProcessManagerService(
    ILoggerFactory loggerFactory,
    HexusConfigurationManager configManager,
    ProcessLogsService processLogsService)
{
    private readonly ILogger<ProcessManagerService> _logger = loggerFactory.CreateLogger<ProcessManagerService>();
    private readonly Dictionary<Process, HexusApplication> _processToApplicationMap = [];
    private readonly Dictionary<string, Process> _applicationToProcessMap = [];

    public SpawnProcessError? StartApplication(HexusApplication application)
    {
        if (IsApplicationRunning(application, out _))
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

        return null;
    }

    public bool StopApplication(HexusApplication application, bool forceStop = false)
    {
        if (!IsApplicationRunning(application, out var process))
            return false;

        application.Status = HexusApplicationStatus.Stopping;

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
            // If the executable is not found, the first is the Linux error, the second the Win32 error
            if (exception.Message.EndsWith("No such file or directory") || exception.Message.EndsWith("The system cannot find the file specified."))
            {
                return (null, SpawnProcessError.NotFound);
            }

            // If the executable can not be accessed, the first is the Linux error, the second the Win32 error
            if (exception.Message.EndsWith("Permission denied") || exception.Message.EndsWith("Access is denied."))
            {
                return (null, SpawnProcessError.PermissionDenied);
            }

            // If the executable is invalid, the first is the Linux error, the second the Win32 error
            if (exception.Message.EndsWith("Exec format error") || exception.Message.EndsWith("The specified executable is not a valid application for this OS platform."))
            {
                return (null, SpawnProcessError.InvalidExecutable);
            }

            // If the command is too long, the first is the Linux error for the arguments, the second one is the Linux error for the file, the third one is the Win32 error
            if (exception.Message.EndsWith("Argument list too long") || exception.Message.EndsWith("File name too long") || exception.Message.EndsWith("The filename or extension is too long."))
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

    #region Exit process event handlers

    // If the application can't live for more then 30 seconds, after the 10 attempts to restart it, it will be considerate crashed
    private static readonly TimeSpan ResetTimeWindow = TimeSpan.FromSeconds(30);
    private const int MaxRestarts = 10;

    private readonly Dictionary<string, ConsequentialRestartsMetadata> _consequentialRestarts = [];

    public bool AbortProcessRestart(HexusApplication application)
    {
        if (!_consequentialRestarts.Remove(application.Name, out var metadata)) return false;

        metadata.ClearConsequentialRestartCancellationTokenSource?.Dispose();
        metadata.AbortRestartCancellationTokenSource.Cancel();
        metadata.AbortRestartCancellationTokenSource.Dispose();

        return true;
    }

    private void AcknowledgeProcessExit(object? sender, EventArgs e)
    {
        if (sender is not Process process || !_processToApplicationMap.TryGetValue(process, out var application))
            return;

        var exitCode = process.ExitCode;

        processLogsService.ProcessApplicationLog(application, LogType.SYSTEM, string.Format(null, ProcessLogsService.ApplicationStoppedLog, exitCode));

        process.Close();
        process.Dispose();

        LogAcknowledgeProcessExit(_logger, application.Name, exitCode);
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

        var status = _consequentialRestarts.GetValueOrDefault(application.Name, new ConsequentialRestartsMetadata());

        status.Count++;
        status.ClearConsequentialRestartCancellationTokenSource?.Dispose();
        status.ClearConsequentialRestartCancellationTokenSource = new CancellationTokenSource(ResetTimeWindow);

        _consequentialRestarts[application.Name] = status;
        ClearApplicationStateOnExit(sender, e);

        if (status.Count > MaxRestarts)
        {
            LogCrashedApplication(_logger, application.Name, status.Count, ResetTimeWindow.TotalSeconds);

            status.ClearConsequentialRestartCancellationTokenSource.Dispose();
            _consequentialRestarts.Remove(application.Name, out _);

            application.Status = HexusApplicationStatus.Crashed;
            configManager.SaveConfiguration();

            return;
        }

        application.Status = HexusApplicationStatus.Restarting;

        var delay = CalculateDelay(status.Count);
        status.ClearConsequentialRestartCancellationTokenSource.Token.Register(ClearConsequentialRestarts, application.Name);

        LogRestartAttemptDelay(_logger, application.Name, delay.TotalSeconds);

        Task.Delay(delay, status.AbortRestartCancellationTokenSource.Token).ContinueWith(delayTask =>
        {
            // If the task was aborted we need to not restart the application.
            if (!delayTask.IsCompletedSuccessfully) return;

            StartApplication(application);
            configManager.SaveConfiguration();
        });
    }

    private void ClearConsequentialRestarts(object? state)
    {
        if (state is not string name)
            return;

        _consequentialRestarts.Remove(name, out var status);
        status.ClearConsequentialRestartCancellationTokenSource?.Dispose();

        LogConsequentialRestartsStop(_logger, status.Count, name);
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

    private record struct ConsequentialRestartsMetadata(
        int Count,
        CancellationTokenSource? ClearConsequentialRestartCancellationTokenSource,
        CancellationTokenSource AbortRestartCancellationTokenSource)
    {
        public ConsequentialRestartsMetadata() : this(0, null, new CancellationTokenSource())
        {
        }
    }

    #endregion

    [LoggerMessage(LogLevel.Error, "Failed to get the CTRL Routine Procedure address, sending signals to processes will not work.")]
    private static partial void LogFailedToGetCtrlProcedureAddress(ILogger logger);

    [LoggerMessage(LogLevel.Warning, "Application \"{Name}\" has exited for {MaxRestarts} times in the time window ({TimeWindow} seconds). It will be considered crashed")]
    private static partial void LogCrashedApplication(ILogger logger, string name, int maxRestarts, double timeWindow);

    [LoggerMessage(LogLevel.Debug, "Acknowledging about \"{Name}\" exiting with code: {ExitCode}")]
    private static partial void LogAcknowledgeProcessExit(ILogger logger, string name, int exitCode);

    [LoggerMessage(LogLevel.Debug, "After {Restarts} restarts, application \"{Name}\" stopped restarting")]
    private static partial void LogConsequentialRestartsStop(ILogger logger, int restarts, string name);

    [LoggerMessage(LogLevel.Debug, "Attempting to restart application \"{Name}\", waiting for {Seconds} seconds before restarting")]
    private static partial void LogRestartAttemptDelay(ILogger logger, string name, double seconds);

    [LoggerMessage(LogLevel.Debug, "Unable to stop process")]
    private static partial void LogFailedApplicationStop(ILogger logger, Exception exception);
}
