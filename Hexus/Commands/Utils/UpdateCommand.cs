using Octokit;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
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

    private static async Task<int> Handler(InvocationContext context)
    {
        var ci = context.ParseResult.GetValueForOption(CiBuildOption);
        var ct = context.GetCancellationToken();

        var update = await CheckForUpdate(ci);

        if (!ci)
        {
            if (!update.NeedsUpdate)
            {
                PrettyConsole.Out.MarkupLine("There is [mediumspringgreen]no update[/] available. Your are already up to date");
                return 0;
            }

            PrettyConsole.Out.MarkupLineInterpolated($"Version [steelblue1]{update.Version}[/] is available.");
        }


        var file = $"{RuntimeInformation.RuntimeIdentifier}-{Variant}.tar.gz";

        var daemonRunning = await HttpInvocation.CheckForRunningDaemon(ct);
        var oldFilesRequired = OperatingSystem.IsWindows();

        var link = ci
            ? $"https://github.com/Fleny113/Hexus/releases/download/ci/{file}"
            : $"https://github.com/Fleny113/Hexus/releases/download/{update.Version}/{file}";

        var currentPath = Environment.ProcessPath;
        var currentDir = Path.GetDirectoryName(currentPath);

        if (currentPath is null || currentDir is null)
        {
            PrettyConsole.Error.MarkupLine("There [indianred1]was an error[/] fetching the Hexus files path.");
            return 1;
        }

        if (!CleanOldFiles(currentDir))
        {
            PrettyConsole.Error.MarkupLine("[lightsteelblue].old[/] files where found and they [red1]couldn't be removed[/]. [aquamarine1]Restart the daemon[/] and try again");
            return 1;
        }

        PrettyConsole.Out.MarkupLineInterpolated($"[mediumpurple]Downloading[/] the update from \"[link]{link}[/]\".");

        using var httpClient = new HttpClient();
        using var request = await httpClient.GetAsync(link, ct);

        if (!request.IsSuccessStatusCode)
        {
            var body = await request.Content.ReadAsStringAsync(ct);
            PrettyConsole.Error.MarkupLineInterpolated($"There [indianred1]was an error[/] fetching the update. HTTP status code: {request.StatusCode}, body: \"{body}\"");
            return 1;
        }

        await using var stream = await request.Content.ReadAsStreamAsync(ct);
        await using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
        await using var tarReader = new TarReader(gzipStream);

        while (await tarReader.GetNextEntryAsync(cancellationToken: ct) is { } entry)
        {
            var path = Path.Combine(currentDir, entry.Name);

            if (entry.EntryType is TarEntryType.Directory)
            {
                Directory.CreateDirectory(path);
                continue;
            }

            // On Windows we need to rename the files to change the handles and being able to update the files
            if (oldFilesRequired)
            {
                try
                {
                    File.Move(path, $"{path}.old", overwrite: true);
                }
                catch (FileNotFoundException ex) when (ex.Message == $"Could not find file '{path}'.")
                {
                    // Ignore the exception
                }
            }

            await entry.ExtractToFileAsync(path, overwrite: true, cancellationToken: ct);
        }

        if (daemonRunning)
        {
            PrettyConsole.Out.MarkupLine("The [lightcoral]daemon[/] could not be updated as it is running. [aquamarine1]Restart the daemon[/] to finish the update.");
            return 0;
        }

        PrettyConsole.Out.MarkupLine("Update [springgreen1]done[/].");

        return 0;
    }

    private static async Task<(bool NeedsUpdate, string Version)> CheckForUpdate(bool ci)
    {
        if (ci)
        {
            // As fair as i known, there isn't a way to use [MaybeNullWhen] with tuples
            return (false, null!);
        }

        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version ?? throw new Exception("Couldn't get version from assembly");

        // .NET uses a "Major.Minor.Build.Revision" versioning system for the Version class. Hexus uses SemVer, so we need to change the format
        var versionString = assemblyVersion.ToString(3);
        var currentVersion = Version.Parse(versionString);

        var github = new GitHubClient(new ProductHeaderValue("Hexus", versionString));
        var latestRelease = await github.Repository.Release.GetLatest("Fleny113", "Hexus");

        if (!Version.TryParse(latestRelease.TagName, out var tagVersion))
        {
            throw new Exception("Couldn't parse the tag name as a version");
        }

        // The tags are named using the version they refer to ("0.2.1", "0.3.0") so we can just compare the tag name with the current running version
        var needsUpdate = tagVersion > currentVersion;

        return (needsUpdate, latestRelease.TagName);
    }

    private static bool CleanOldFiles(string searchPath)
    {
        var oldFiles = Directory.EnumerateFiles(searchPath, "*.old", SearchOption.AllDirectories);

        try
        {
            foreach (var file in oldFiles)
            {
                File.Delete(file);
            }
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return true;
    }
}
