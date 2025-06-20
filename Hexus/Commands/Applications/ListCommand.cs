using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts.Responses;
using Humanizer;
using Humanizer.Localisation;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net.Http.Json;

namespace Hexus.Commands.Applications;

internal static class ListCommand
{
    public static readonly Command Command = new("list", "List application running under Hexus");

    static ListCommand()
    {
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            context.ExitCode = 1;
            return;
        }

        var listRequest = await HttpInvocation.GetAsync("Getting application list", "/list", ct);

        if (!listRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(listRequest, ct);
            context.ExitCode = 1;
            return;
        }

        var applications = await listRequest.Content.ReadFromJsonAsync<IEnumerable<ApplicationResponse>>(HttpInvocation.JsonSerializerContext.IEnumerableApplicationResponse, ct);
        Debug.Assert(applications is not null);

        var table = new Table();

        table
            .Title("[deepskyblue3]Hexus applications[/]")
            .Border(TableBorder.Simple)
            .BorderColor(Color.Gold1)
            .AddColumns(
                new TableColumn("[cornflowerblue]Name[/]").Centered(),
                new TableColumn("[palegreen1]Status[/]").Centered(),
                new TableColumn("[lightsalmon1]Uptime[/]").Centered(),
                new TableColumn("[slateblue1]PID[/]").Centered(),
                new TableColumn("[lightslateblue]Cpu Usage[/]").Centered(),
                new TableColumn("[skyblue1]Memory Usage[/]").Centered()
            );

        foreach (var application in applications)
        {
            var isStopped = application.ProcessId == 0;

            table.AddRow(
                application.Name.EscapeMarkup(),
                $"[{GetStatusColor(application.Status)}]{application.Status}[/]",
                isStopped ? "N/A" : $"{application.ProcessUptime.Humanize(minUnit: TimeUnit.Second, precision: 1)}",
                isStopped ? "N/A" : $"{application.ProcessId}",
                isStopped ? "N/A" : $"{application.CpuUsage}%",
                isStopped ? "N/A" : $"{application.MemoryUsage.Bytes().Humanize()}"
            );
        }

        if (table.Rows.Count == 0)
        {
            table.AddEmptyRow();
            table.Caption("[italic grey39]it's quiet here...\nAdd a new application using the new command[/]");
        }

        PrettyConsole.Out.Write(table);
    }

    internal static Color GetStatusColor(HexusApplicationStatus status) => status switch
    {
        HexusApplicationStatus.Crashed => Color.LightSalmon3,
        HexusApplicationStatus.Exited => Color.OrangeRed1,
        HexusApplicationStatus.Running => Color.Aquamarine1,
        HexusApplicationStatus.Stopping => Color.IndianRed1,
        HexusApplicationStatus.Restarting => Color.SkyBlue1,
        _ => throw new ArgumentOutOfRangeException(nameof(status), "The requested status is not mapped to a color"),
    };
}
