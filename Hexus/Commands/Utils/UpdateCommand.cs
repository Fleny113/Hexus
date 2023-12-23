using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Hexus.Commands.Utils;

internal static class UpdateCommand
{
    private static readonly Option<bool> CiBuildOption = new("--ci", "Use a build from ci");
    
    public static readonly Command Command = new("update", "Update hexus to the latest version")
    {
        CiBuildOption,
    };
    
#if SELF_CONTAINED
    private const string Variant = "self-contained";
#else
    private const string Variant = "runtime";
#endif
    
    static UpdateCommand()
    {
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var ci = context.ParseResult.GetValueForOption(CiBuildOption);
        var ct = context.GetCancellationToken();
        
        if (await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine("The [indianred1]daemon needs to not be running[/] to update hexus. Stop it first using the '[indianred1]daemon[/] [darkseagreen1_1]stop[/]' command.");
            return;
        }

        var file = $"{RuntimeInformation.RuntimeIdentifier}-{Variant}.tar.gz";
        
        var link = ci 
            ? $"https://github.com/Fleny113/Hexus/releases/download/ci/{file}" 
            : $"https://github.com/Fleny113/test-ci/releases/latest/download/{file}";
        
        var currentPath = Environment.ProcessPath;

        if (currentPath is null)
        {
            PrettyConsole.Error.MarkupLine("There [indianred1]was an error[/] fetching the current executable path.");
            return;
        }
        
        PrettyConsole.Out.MarkupLineInterpolated($"Downloading the updated binary from \"{link}\".");
        
        using var httpClient = new HttpClient();
        using var tar = await httpClient.GetAsync(link, ct);

        if (!tar.IsSuccessStatusCode)
        {
            var body = await tar.Content.ReadAsStringAsync(ct);
            PrettyConsole.Error.MarkupLineInterpolated($"There [indianred1]was an error[/] fetching the updated binary. HTTP status code: {tar.StatusCode}, body: \"{body}\"");
            return;
        }
        
        await using var stream = await tar.Content.ReadAsStreamAsync(ct);

        await using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
        await using var tarReader = new TarReader(gzipStream);

        var currentFilename = Path.GetFileName(currentPath);
        var tempFileExec = Path.GetTempFileName();
        
        while (await tarReader.GetNextEntryAsync(cancellationToken: ct) is { DataStream: not null } entry)
        {
            // Find the hexus (or hexus.exe) file
            if (!entry.Name.StartsWith("hexus"))
                continue;
            
            await entry.ExtractToFileAsync(tempFileExec, overwrite: true, cancellationToken: ct);
            break;
        }

        // Under windows the file will be locked, so we need to use a script to bypass the file locking
        if (OperatingSystem.IsWindows())
        {
            var tempFileScript = $"{Path.GetTempFileName()}.bat";
            
            // there is a timeout delay to allow for the CLI to exit
            var script = $"""
                          @echo off
                          timeout /t 5 > NUL
                          del "{currentFilename}"
                          move "{tempFileExec}" "{currentFilename}"
                          del "%~f0"
                          """;
            
            await File.WriteAllTextAsync(tempFileScript, script, ct);
            
            PrettyConsole.Out.MarkupLine("[yellow]WARNING[/]: To update hexus a batch script will run to replace the file. Please wait a about 5 seconds before restarting hexus.");
            Process.Start(new ProcessStartInfo(tempFileScript)
            {
                UseShellExecute = false, 
                CreateNoWindow = true,
            });
            
            return;
        }
        
        File.Move(tempFileExec, currentFilename, overwrite: true);
    }
}
