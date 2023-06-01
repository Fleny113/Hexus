using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Hexus.Daemon.Services;

public sealed class ProcessManagerService(ILogger<ProcessManagerService> _logger, IOptions<HexusConfiguration> _options)
{
    private readonly ConcurrentDictionary<int, Process> _processes = new();

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

            // NOTE: If set to UTF8 it may give issues when using the STDIN
            //  ASCII seems to solve the issue
            StandardInputEncoding = Encoding.ASCII,
        };

        var process = Process.Start(processInfo);

        if (process is null)
            return false;

        // Enable the emitting of events and the reading of the STDOUT and STDERR
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Register callbacks
        process.OutputDataReceived += HandleDataReceived;
        process.ErrorDataReceived += HandleDataReceived;
        process.Exited += HandleProcessExited;

        // Wait for the process to start (with a timeout of 30 seconds)
        if (!SpinWait.SpinUntil(() => process.Id is > 0, TimeSpan.FromSeconds(30)))
            return false;

        _processes[application.Id] = process;

        return true;
    }

    /// <summary>Stop the instance of an application</summary>
    /// <param name="id">The id of the application to stop</param>
    /// <returns>Whatever if the application was stopped or not</returns>
    public bool StopApplication(int id)
    {
        if (!_processes.TryGetValue(id, out var process))
            return false;


        // TODO: on UNIX system try sending a signal and not a SIGKILL
        //        maybe on windows try sending a SIGINT/SIGBREAK (??)
        process.Kill();

        if (!_processes.TryRemove(id, out _))
            return false;

        return true;
    }

    public bool IsApplicationRunning(int id) => _processes.ContainsKey(id);

    internal int GetApplicationId() 
    {
        int key = 0;

        while (true)
        {
            if (!_processes.ContainsKey(++key))
                return key;
        }
    }

    #region Process Handlers

    private void HandleDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (sender is not Process process)
            return;

        _logger.LogInformation("{PID} says: '{OutputData}'", process.Id, e.Data);
    }

    private void HandleProcessExited(object? sender, EventArgs e)
    {
        if (sender is not Process process)
            return;

        _logger.LogInformation("{PID} has exited with code: {ExitCode}", process.Id, process.ExitCode);
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Start all application managed by Hexus on startup after the WebHost has started
    /// </summary>
    internal void ApplicationStartup()
    {
        foreach (var application in _options.Value.Applications)
        {
            StartApplication(application);
        }
    }

    /// <summary>
    /// Gracefully shutdown all the applications after the WebHost has stopped
    /// </summary>
    internal void ApplicationShutdown()
    {
        foreach (var process in _processes)
        {
            StopApplication(process.Key);
        }
    }

    #endregion
}
