using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Services;
using Humanizer;
using Spectre.Console;
using System.CommandLine;
using System.Diagnostics;
using System.Net.Http.Json;

namespace Hexus.Commands.Applications;

internal static class ListCommand
{
    public static readonly Command Command = new("list", "List applications running under Hexus");

    static ListCommand()
    {
        Command.SetAction(Handler);
    }

    internal static async Task<int> Handler(ParseResult parseResult, CancellationToken ct)
    {
        var isDaemonRunning = await HttpInvocation.CheckForRunningDaemon(ct);

        if (!isDaemonRunning)
        {
            PrettyConsole.Out.MarkupLine("[blue]Note[/]: Hexus daemon is not running, showing applications from configs\n");
        }

        var applications = isDaemonRunning ? await LoadApplicationsFromDaemon(ct) : LoadApplicationsFromConfig();

        var table = new Table();

        table
            .Title("[deepskyblue3]Hexus applications[/]")
            .Border(TableBorder.Simple)
            .BorderColor(Color.Gold1)
            .AddColumns(
                new TableColumn("[cornflowerblue]Name[/]").Centered(),
                new TableColumn("[palegreen1]Status[/]").Centered(),
                new TableColumn("[darkseagreen4_1]Enabled[/]").Centered(),
                new TableColumn("[lightsalmon1]Uptime[/]").Centered(),
                new TableColumn("[slateblue1]PID[/]").Centered(),
                new TableColumn("[lightslateblue]Cpu Usage[/]").Centered(),
                new TableColumn("[skyblue1]Memory Usage[/]").Centered()
            );

        foreach (var application in applications)
        {
            var hasProcess = application.ProcessId == 0;

            table.AddRow(
                application.Name.EscapeMarkup(),
                $"[{GetStatusColor(application.Status)}]{application.Status}[/]",
                $"{(application.Enabled ? "[green]Yes[/]" : "[red]No[/]")}",
                hasProcess ? "N/A" : $"{application.ProcessUptime.Humanize(minUnit: TimeUnit.Second, precision: 1)}",
                hasProcess ? "N/A" : $"{application.ProcessId}",
                hasProcess ? "N/A" : $"{application.CpuUsage}%",
                hasProcess ? "N/A" : $"{application.MemoryUsage.Bytes().Humanize()}"
            );
        }

        if (table.Rows.Count == 0)
        {
            table.AddEmptyRow();
            table.Caption("[italic grey39]It's quiet here...\nAdd a new application using the new command[/]");
        }

        PrettyConsole.Out.Write(table);

        return 0;
    }

    internal static Color GetStatusColor(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Running => Color.Aquamarine1,
        ApplicationStatus.Stopping => Color.IndianRed1,
        ApplicationStatus.Stopped => Color.OrangeRed1,
        ApplicationStatus.Restarting => Color.SkyBlue1,
        ApplicationStatus.Crashed => Color.LightSalmon3,
        _ => throw new ArgumentOutOfRangeException(nameof(status), "The requested status is not mapped to a color"),
    };

    private static async Task<IEnumerable<ApplicationResponse>> LoadApplicationsFromDaemon(CancellationToken ct)
    {
        var listRequest = await HttpInvocation.GetAsync("Getting application list", "/list", ct);

        if (!listRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(listRequest, ct);
            return [];
        }

        var applications =
            await listRequest.Content.ReadFromJsonAsync<IEnumerable<ApplicationResponse>>(HttpInvocation.JsonSerializerContext.IEnumerableApplicationResponse, ct);
        Debug.Assert(applications is not null);

        return applications;
    }

    private static IEnumerable<ApplicationResponse> LoadApplicationsFromConfig()
    {
        var configurationManager = new HexusConfigurationManager();

        return configurationManager.Applications.Select(kvp => kvp.Value.MapToResponse(new ApplicationStatistics(
            ProcessUptime: TimeSpan.Zero,
            ProcessId: 0,
            Status: ApplicationStatus.Stopped,
            CpuUsage: 0,
            MemoryUsage: 0
        )));
    }
}
