using Hexus.Daemon.Contracts;
using Humanizer;
using Humanizer.Localisation;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net.Http.Json;

namespace Hexus.Commands.Applications;

internal static class InfoCommand
{
    private static readonly Argument<string> NameArgument = new("name", "The name of the application");

    public static readonly Command Command = new("info", "Get the information for an application")
    {
        NameArgument,
    };

    static InfoCommand()
    {
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return;
        }
        
        var infoRequest = await HttpInvocation.HttpClient.GetAsync($"/{name}", ct);
        
        if (!infoRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(infoRequest, ct);
            return;
        }
        
        var application = await infoRequest.Content.ReadFromJsonAsync<HexusApplicationResponse>(HttpInvocation.JsonSerializerOptions, ct);
    
        Debug.Assert(application is not null);

        var isStopped = application.ProcessId == 0;
        
        PrettyConsole.Out.MarkupLine($"""
            Application configuration:
            - [cornflowerblue]Name[/]: {application.Name.EscapeMarkup()}
            - [salmon1]Executable file[/]: [link]{application.Executable.EscapeMarkup()}[/]
            - [lightseagreen]Arguments[/]: {(string.IsNullOrWhiteSpace(application.Arguments) ? "[italic gray39]<No arguments specified>[/]" : application.Arguments.EscapeMarkup())}
            - [plum2]WorkingDirectory[/]: [link]{application.WorkingDirectory.EscapeMarkup()}[/]
            
            Current status:
            - [palegreen1]Status[/]: [{ListCommand.GetStatusColor(application.Status)}]{application.Status}[/]
            - [lightsalmon1]Uptime[/]: {(isStopped ? "N/A" : $"{application.ProcessUptime.Humanize(minUnit: TimeUnit.Second, maxUnit: TimeUnit.Year, precision: 7)}")}
            - [slateblue1]PID[/]: {(isStopped ? "N/A" : application.ProcessId)}
            - [lightslateblue]CPU Usage[/]: {(isStopped ? "N/A" : $"{application.CpuUsage}%")}
            - [skyblue1]Memory Usage[/]: {(isStopped ? "N/A" : application.MemoryUsage.Bytes().Humanize())}
            """);
    }
}
