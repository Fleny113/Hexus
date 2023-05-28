using System.Diagnostics;
using System.Text;

namespace Hexus.Daemon.Services;

public sealed class ProcessManagerService(ILogger<ProcessManagerService> _logger)
{
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

        var process = Process.Start(processInfo) ?? throw new InvalidOperationException("Unable to start the process");

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

        // TODO: to something

        return true;
    }

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
}
