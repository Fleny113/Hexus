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

internal static class InfoCommand
{
    private static readonly Argument<string> NameArgument = new("name") { Description = "The name(s) of the application(s) to get the info for", };

    private static readonly Option<bool> ShowEnvironmentVariables = new("--show-environment", "-e")
    {
        Description = "Show the environment variables the application has set",
    };

    public static readonly Command Command = new("info", "Get the information for an application") { NameArgument, ShowEnvironmentVariables, };

    static InfoCommand()
    {
        Command.SetAction(Handler);
    }

    private static async Task<int> Handler(ParseResult parseResult, CancellationToken ct)
    {
        var name = parseResult.GetRequiredValue(NameArgument);
        var showEnv = parseResult.GetValue(ShowEnvironmentVariables);

        var isDaemonRunning = await HttpInvocation.CheckForRunningDaemon(ct);

        if (!isDaemonRunning)
        {
            PrettyConsole.Out.MarkupLine("[blue]Note[/]: Hexus daemon is not running, showing application from config\n");
        }

        var application = isDaemonRunning ? await LoadApplicationFromDaemon(name, ct) : LoadApplicationFromConfig(name);

        if (application is null)
        {
            PrettyConsole.Error.MarkupLine($"[red]Error[/]: Application [cornflowerblue]{name}[/] not found");
            return 1;
        }

        var isStopped = application.ProcessId == 0;
        var environmentVariables = showEnv
            ? $"\n{string.Join("\n", application.EnvironmentVariables.Select(kvp => $"  - [tan]{kvp.Key}[/]: {kvp.Value}"))}"
            : "[italic gray39]Use the --show-environment option to list them[/]";

        PrettyConsole.OutLimitlessWidth.MarkupLine(
            $"""
             Application configuration:
             - [cornflowerblue]Name[/]: {application.Name.EscapeMarkup()}
             - [salmon1]Executable file[/]: [link]{application.Executable.EscapeMarkup()}[/]
             - [lightseagreen]Arguments[/]: {(string.IsNullOrWhiteSpace(application.Arguments) ? "[italic gray39]No arguments specified[/]" : application.Arguments.EscapeMarkup())}
             - [plum2]Working Directory[/]: [link]{application.WorkingDirectory.EscapeMarkup()}[/]
             - [darkseagreen4_1]Enabled[/]: {(application.Enabled ? "[green]Yes[/]" : "[red]No[/]")}
             - [lightgoldenrod2_1]Note[/]: {(string.IsNullOrWhiteSpace(application.Note) ? "[italic gray39]No note added[/]" : application.Note)}
             - [aquamarine1]Environment variables[/]: {environmentVariables}
             - [skyblue2]Memory limit[/]: {(application.MemoryLimit == 0 ? "[italic gray39]No limit set[/]" : $"{application.MemoryLimit.Bytes().Humanize()}")}

             Current status:
             - [palegreen1]Status[/]: [{ListCommand.GetStatusColor(application.Status)}]{application.Status}[/]
             - [lightsalmon1]Uptime[/]: {(isStopped ? "N/A" : $"{application.ProcessUptime.Humanize(minUnit: TimeUnit.Second, maxUnit: TimeUnit.Week, precision: 7)}")}
             - [slateblue1]PID[/]: {(isStopped ? "N/A" : application.ProcessId)}
             - [lightslateblue]CPU Usage[/]: {(isStopped ? "N/A" : $"{application.CpuUsage}%")}
             - [skyblue1]Memory Usage[/]: {(isStopped ? "N/A" : application.MemoryUsage.Bytes().Humanize())}
             """);

        return 0;
    }

    private static async Task<ApplicationResponse?> LoadApplicationFromDaemon(string name, CancellationToken ct)
    {
        var infoRequest = await HttpInvocation.GetAsync("Gathering information", $"/{name}", ct);

        if (!infoRequest.IsSuccessStatusCode)
        {
            await HttpInvocation.HandleFailedHttpRequestLogging(infoRequest, ct);
            return null;
        }

        var application = await infoRequest.Content.ReadFromJsonAsync(HttpInvocation.JsonSerializerContext.ApplicationResponse, ct);

        Debug.Assert(application is not null);

        return application;
    }

    private static ApplicationResponse? LoadApplicationFromConfig(string name)
    {
        var configurationManager = new HexusConfigurationManager();

        var app = configurationManager.Applications.GetValueOrDefault(name);

        return app?.MapToResponse(new ApplicationStatistics(
            ProcessUptime: TimeSpan.Zero,
            ProcessId: 0,
            Status: ApplicationStatus.Stopped,
            CpuUsage: 0,
            MemoryUsage: 0
        ));
    }
}
