using Hexus.Daemon.Contracts;
using Humanizer;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net.Http.Json;

namespace Hexus.Commands.Applications;

public static class InfoCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application");

    public static readonly Command Command = new("info", "Get the information for an application")
    {
        NameArgument,
    };

    static InfoCommand()
    {
        Command.SetHandler(Handle);
    }

    private static async Task Handle(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return;
        }
        
        var infoRequest = await HttpInvocation.HttpClient.GetAsync($"/{name}", ct);
        
        var application = await infoRequest.Content.ReadFromJsonAsync<HexusApplicationResponse>(HttpInvocation.JsonSerializerOptions, ct);
    
        Debug.Assert(application is not null);
        
        PrettyConsole.Out.MarkupLineInterpolated($"""
            Application configuration:
            - [cornflowerblue]Name[/]: {application.Name}
            - [salmon1]Executable file[/]: [link]{application.Executable}[/]
            - [lightseagreen]Arguments[/]: {application.Arguments}
            - [plum2]WorkingDirectory[/]: [link]{application.WorkingDirectory}[/]
            
            Current status:
            - [palegreen1]Status[/]: [{ListCommand.GetStatusColor(application.Status)}]{application.Status}[/]
            - [slateblue3]PID[/]: {application.ProcessId}
            - [lightslateblue]CPU Usage[/]: {application.CpuUsage}%
            - [skyblue1]Memory Usage[/]: {application.MemoryUsage.Bytes().Humanize()}
            """);
    }
}
