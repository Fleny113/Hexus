using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Interop;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Hexus.Daemon.Services;

internal partial class ProcessManagerService(ILogger<ProcessManagerService> logger, HexusConfigurationManager configManager)
{
    internal ConcurrentDictionary<Process, HexusApplication> Applications { get; } = new();

    /// <summary> Start an instance of the application</summary>
    /// <param name="application">The application to start</param>
    /// <returns>Whatever if the application was started or not</returns>
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
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            // NOTE: If set to UTF8 it may give issues when using the STDIN, ASCII seems to solve the issue
            StandardInputEncoding = Encoding.ASCII,
        };

        processInfo.Environment.Clear();

        foreach (var (key, value) in application.EnvironmentVariables)
            processInfo.Environment.Add(key, value);

        var process = Process.Start(processInfo);

        if (process is null or { HasExited: true })
            return false;

        application.Process = process;
        Applications[process] = application;

        ProcessApplicationLog(application, LogType.System, "-- Application started --");

        // Enable the emitting of events and the reading of the STDOUT and STDERR
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Register callbacks
        process.OutputDataReceived += HandleStdOutLogs;
        process.ErrorDataReceived += HandleStdErrLogs;

        process.Exited += AcknowledgeProcessExit;
        process.Exited += HandleProcessRestart;

        application.Status = HexusApplicationStatus.Running;
        configManager.SaveConfiguration();

        return true;
    }

    /// <summary>Stop the instance of an application</summary>
    /// <param name="name">The name of the application to stop</param>
    /// <param name="forceStop">Force the stopping of the application via a force kill</param>
    /// <returns>If the application was running</returns>
    public bool StopApplication(string name, bool forceStop = false)
    {
        if (!IsApplicationRunning(name, out var application) || application.Process is null)
            return false;

        // Remove the restart event handler, or else it will restart the process as soon as it stops
        application.Process.Exited -= HandleProcessRestart;

        StopProcess(application.Process, forceStop);
        application.Status = HexusApplicationStatus.Exited;

        // If the daemon is shutting down we don't want to save, or else when the daemon is booted up again, all the applications will be marked as stopped
        if (!HexusLifecycle.IsDaemonStopped)
            configManager.SaveConfiguration();

        return true;
    }

    /// <summary>Given a name of an application check if it exists, is running and has an attached process running</summary>
    /// <param name="name">The name of the application</param>
    /// <param name="application">The application returned with the same <paramref name="name"/> string</param>
    /// <returns>If the application is running</returns>
    public bool IsApplicationRunning(string name, [NotNullWhen(true)] out HexusApplication? application) =>
        configManager.Configuration.Applications.TryGetValue(name, out application) && IsApplicationRunning(application);

    /// <summary>Check if an application exists, is running and has an attached process running</summary>
    /// <param name="application">The nullable instance of an <see cref="HexusApplication" /></param>
    /// <returns>If the application is running</returns>
    public static bool IsApplicationRunning([NotNullWhen(true)] HexusApplication? application)
        => application is { Status: HexusApplicationStatus.Running, Process.HasExited: false };

    /// <summary>Send a message into the Standard Input (STDIN) of an application</summary>
    /// <param name="name">The name of the application</param>
    /// <param name="text">The text to send into the STDIN</param>
    /// <param name="newLine">Whatever or not to append an \n to the text</param>
    /// <returns>Whatever or not if the operation was successful or not</returns>
    public bool SendToApplication(string name, ReadOnlySpan<char> text, bool newLine = true)
    {
        if (!IsApplicationRunning(name, out var application) || application.Process is null)
            return false;

        if (newLine)
            application.Process.StandardInput.WriteLine(text);
        else
            application.Process.StandardInput.Write(text);

        return true;
    }

    private void StopProcess(Process process, bool forceStop)
    {
        if (forceStop)
        {
            KillProcess(process, true);
            return;
        }

        // NativeSendSignal can send -1 if the UNIX kill call returns an error
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

    #region Log process events handlers

    private void ProcessApplicationLog(HexusApplication application, LogType logType, string message)
    {
        if (logType != LogType.System)
            LogApplicationOutput(logger, application.Name, message);

        var applicationLog = new ApplicationLog(DateTimeOffset.UtcNow, logType, message);

        application.LogChannels.ForEach(channel => channel.Writer.TryWrite(applicationLog));
        application.LogSemaphore.Wait();

        try
        {
            File.AppendAllText(
                $"{EnvironmentHelper.LogsDirectory}/{application.Name}.log",
                $"[{applicationLog.Date:O},{applicationLog.LogType.Name}] {applicationLog.Text}{Environment.NewLine}"
            );
        }
        finally
        {
            application.LogSemaphore.Release();
        }
    }

    private void HandleStdOutLogs(object? sender, DataReceivedEventArgs e)
    {
        if (sender is not Process process || e.Data is null || !Applications.TryGetValue(process, out var application))
            return;

        ProcessApplicationLog(application, LogType.StdOut, e.Data);
    }

    private void HandleStdErrLogs(object? sender, DataReceivedEventArgs e)
    {
        if (sender is not Process process || e.Data is null || !Applications.TryGetValue(process, out var application))
            return;

        ProcessApplicationLog(application, LogType.StdErr, e.Data);
    }

    #endregion

    #region Exit process event handlers

    // If the application can't live for more then 30 seconds, after the 10 attempts to restart it, it will be considerate crashed
    private static readonly TimeSpan ResetTimeWindow = TimeSpan.FromSeconds(30);
    private const int MaxRestarts = 10;

    private readonly ConcurrentDictionary<string, (int Restarts, CancellationTokenSource? CancellationTokenSource)> _consequentialRestarts = new();

    private void AcknowledgeProcessExit(object? sender, EventArgs e)
    {
        if (sender is not Process process || !Applications.TryRemove(process, out var application))
            return;

        var exitCode = process.ExitCode;

        ProcessApplicationLog(application, LogType.System, $"-- Application stopped [Exit code: {exitCode}] --");

        application.Process?.Close();
        application.Process = null;

        application.CpuStatsMap.Clear();
        application.LastCpuUsage = 0;

        LogAcknowledgeProcessExit(logger, application.Name, exitCode);
    }

    private void HandleProcessRestart(object? sender, EventArgs e)
    {
        if (sender is not Process process || !Applications.TryGetValue(process, out var application))
            return;

        var status = _consequentialRestarts.GetValueOrDefault(application.Name, (0, null));

        status.Restarts++;
        status.CancellationTokenSource?.Dispose();
        status.CancellationTokenSource = new CancellationTokenSource(ResetTimeWindow);

        _consequentialRestarts[application.Name] = status;

        if (status.Restarts > MaxRestarts)
        {
            LogCrashedApplication(logger, application.Name, status.Restarts, ResetTimeWindow.TotalSeconds);

            status.CancellationTokenSource.Dispose();
            _consequentialRestarts.TryRemove(application.Name, out _);

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

        _consequentialRestarts.TryRemove(name, out var status);
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

    [LoggerMessage(LogLevel.Trace, "Application \"{Name}\" says: '{OutputData}'")]
    private static partial void LogApplicationOutput(ILogger logger, string name, string outputData);
}
