using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Humanizer;
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
        Command.SetHandler(Handle);    
    }

    private static async Task Handle(InvocationContext context)
    {
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return;
        }

        var listRequest = await HttpInvocation.HttpClient.GetAsync("/list", ct);

        var applications = await listRequest.Content.ReadFromJsonAsync<IEnumerable<HexusApplicationResponse>>(HttpInvocation.JsonSerializerOptions, ct);
    
        Debug.Assert(applications is not null);
        
        var table = new Table();

        table.Border(TableBorder.Simple);
        table.BorderColor(Color.Gold1);
        table.Title("[deepskyblue3]Hexus applications[/]");
        
        table.AddColumns(
            new TableColumn("[cornflowerblue]Name[/]").Centered(),
            new TableColumn("[palegreen1]Status[/]").Centered(),
            new TableColumn("[slateblue3]PID[/]").Centered(),
            new TableColumn("[lightslateblue]Cpu Usage[/]").Centered(),
            new TableColumn("[skyblue1]Memory Usage[/]").Centered()
        );

        foreach (var application in applications)
        {
            table.AddRow(
                application.Name,
                $"[{GetStatusColor(application.Status)}]{application.Status}[/]",
                $"{application.ProcessId}",
                $"{application.CpuUsage}%",
                $"{application.MemoryUsage.Bytes().Humanize()}"
            );
        }
        
        PrettyConsole.Out.Write(table);
    }

    internal static Color GetStatusColor(HexusApplicationStatus status) => status switch
    {
        HexusApplicationStatus.Crashed => Color.LightSalmon3,
        HexusApplicationStatus.Exited => Color.OrangeRed1,
        HexusApplicationStatus.Running => Color.Aquamarine1,
        _ => throw new ArgumentOutOfRangeException(nameof(status), "The requested status is not mapped to a color"),
    };
}
