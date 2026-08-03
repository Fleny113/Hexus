using Hexus.Configuration;
using Spectre.Console;
using System.CommandLine;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;

namespace Hexus.Commands.Applications;

internal static class EditCommand
{
    private static readonly Argument<string> NameArgument = new("name")
    {
        Description = "The name of the application to edit",
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Command Command = new("edit", "Edit an exiting application")
    {
        NameArgument,
    };

    static EditCommand()
    {
        Command.SetAction(Handler);
    }

    private static async Task<int> Handler(ParseResult parseResult, CancellationToken ct)
    {
        var name = parseResult.GetRequiredValue(NameArgument);

        var configFile = Path.Combine(HexusPaths.ApplicationsConfigDirectory, $"{name}.toml");

        var couldEdit = false;

        foreach (var editor in GetEditors())
        {
            var success = await StartEditor(editor, configFile, ct);
            if (success)
            {
                couldEdit = true;
                break;
            }
        }

        if (!couldEdit)
        {
            PrettyConsole.Error.WriteLine("Could not find a suitable editor. Please set an editor in VISUAL or EDITOR.");
            return 1;
        }

        var actOnDaemon = await HttpInvocation.CheckForRunningDaemon(ct) &&
                          await PrettyConsole.Out.ConfirmAsync("The daemon is running. Do you want to reload the application now?", true, ct);

        if (!actOnDaemon)
        {
            return 0;
        }

        PrettyConsole.Out.WriteLine();
        PrettyConsole.Out.WriteLine("Reloading the daemon configuration to apply the changes...");

        var restartRequest = await HttpInvocation.PostAsJsonAsync<string[]>(
            "Reloading config for the edited apps", "/daemon/reload", [name],
            HttpInvocation.JsonSerializerContext, ct);

        if (!restartRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(restartRequest, ct);
            return 1;
        }

        var reloadResult = await restartRequest.Content.ReadFromJsonAsync(HttpInvocation.JsonSerializerContext.ReloadResult, ct);

        if (reloadResult is null)
        {
            PrettyConsole.Error.MarkupLine("Failed to parse the reload result.");
            return 1;
        }

        return HttpInvocation.LogReloadResult(reloadResult) ? 1 : 0;
    }

    private static async Task<bool> StartEditor(string editor, string configFile, CancellationToken ct)
    {
        try
        {
            var process = Process.Start(editor, [configFile]);

            if (process is null) return false;

            await process.WaitForExitAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return false;
        }
    }

    private static List<string> GetEditors()
    {
        var editors = new List<string>();

        if (Environment.GetEnvironmentVariable("VISUAL") is { Length: > 0 } visual)
            editors.Add(visual);

        if (Environment.GetEnvironmentVariable("EDITOR") is { Length: > 0 } editor)
            editors.Add(editor);

        if (OperatingSystem.IsWindows())
            editors.Add("notepad");
        else
            editors.AddRange(["nano", "vim", "vi"]);

        return editors;
    }
}
