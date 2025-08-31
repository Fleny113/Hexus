using Humanizer;
using Humanizer.Localisation;
using Spectre.Console;
using System.CommandLine;
using System.Diagnostics;
using System.Net.Http.Json;

namespace Hexus.Commands.Applications;

internal static class InfoCommand
{
    private static readonly Argument<string> NameArgument = new("name")
    {
        Description = "The name(s) of the application(s) to get the info for",
    };

    private static readonly Option<bool> ShowEnvironmentVariables = new("--show-environment", "-e")
    {
        Description = "Show the environment variables the application has set"
    };

    public static readonly Command Command = new("info", "Get the information for an application")
    {
        NameArgument,
        ShowEnvironmentVariables,
    };

    static InfoCommand()
    {
        Command.SetAction(Handler);
    }

    private static async Task<int> Handler(ParseResult parseResult, CancellationToken ct)
    {
        var name = parseResult.GetRequiredValue(NameArgument);
        var showEnv = parseResult.GetValue(ShowEnvironmentVariables);

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return 1;
        }

        var infoRequest = await HttpInvocation.GetAsync("Gathering information", $"/{name}", ct);

        if (!infoRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(infoRequest, ct);
            return 1;
        }

        var application = await infoRequest.Content.ReadFromJsonAsync(HttpInvocation.JsonSerializerContext.ApplicationResponse, ct);

        Debug.Assert(application is not null);

        var isStopped = application.ProcessId == 0;
        var environmentVariables = showEnv
            ? $"\n{string.Join("\n", application.EnvironmentVariables.Select(kvp => $"  - [tan]{kvp.Key}[/]: {kvp.Value}"))}"
            : "[italic gray39]Use the --show-environment option to list them[/]";

        var isGlobalMemoryLimit = application.MemoryLimit is null;
        var memoryLimit = (long?)application.MemoryLimit ?? (long)Configuration.HexusConfiguration.MemoryLimit;
        
        PrettyConsole.OutLimitlessWidth.MarkupLine($"""
            Application configuration:
            - [cornflowerblue]Name[/]: {application.Name.EscapeMarkup()}
            - [salmon1]Executable file[/]: [link]{application.Executable.EscapeMarkup()}[/]
            - [lightseagreen]Arguments[/]: {(string.IsNullOrWhiteSpace(application.Arguments) ? "[italic gray39]No arguments specified[/]" : application.Arguments.EscapeMarkup())}
            - [plum2]Working Directory[/]: [link]{application.WorkingDirectory.EscapeMarkup()}[/]
            - [lightgoldenrod2_1]Note[/]: {(string.IsNullOrWhiteSpace(application.Note) ? "[italic gray39]No note added[/]" : application.Note)}
            - [aquamarine1]Environment variables[/]: {environmentVariables}
            - [skyblue2]Memory limit[/]: {(application.MemoryLimit == 0 ? "[italic gray39]No limit set[/]" : $"{memoryLimit.Bytes().Humanize()} {(isGlobalMemoryLimit ? "[italic gray39](global limit)[/]" : "")}")}
            
            Current status:
            - [palegreen1]Status[/]: [{ListCommand.GetStatusColor(application.Status)}]{application.Status}[/]
            - [lightsalmon1]Uptime[/]: {(isStopped ? "N/A" : $"{application.ProcessUptime.Humanize(minUnit: TimeUnit.Second, maxUnit: TimeUnit.Week, precision: 7)}")}
            - [slateblue1]PID[/]: {(isStopped ? "N/A" : application.ProcessId)}
            - [lightslateblue]CPU Usage[/]: {(isStopped ? "N/A" : $"{application.CpuUsage}%")}
            - [skyblue1]Memory Usage[/]: {(isStopped ? "N/A" : application.MemoryUsage.Bytes().Humanize())}
            """);

        return 0;
    }
}
