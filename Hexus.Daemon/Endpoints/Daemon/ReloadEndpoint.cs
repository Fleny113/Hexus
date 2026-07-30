using EndpointMapper;
using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Immutable;

namespace Hexus.Daemon.Endpoints.Daemon;

internal class ReloadEndpoint : IEndpoint, IRegisterEndpoint
{
    public static void Register(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/daemon/reload", Handle);
    }

    public static Ok<ReloadResult> Handle(
        [FromServices] HexusConfigurationManager configurationManager,
        [FromServices] IEnumerable<IConfigRelodable> relodableServices,
        [FromQuery] string[] applicationNames
    )
    {
        var oldConfig = new ConfigurationSnapshot(configurationManager.DaemonConfiguration, configurationManager.Applications.ToImmutableDictionary());

        ConfigurationProblems reloadProblems;

        if (applicationNames.Length > 0)
        {
            reloadProblems = applicationNames.Aggregate(new ConfigurationProblems([], []), (acc, appName) =>
            {
                var result = configurationManager.Reload(appName);
                return new ConfigurationProblems(
                    Warnings: acc.Warnings.Concat(result.Warnings),
                    Errors: acc.Errors.Concat(result.Errors)
                );
            });
        }
        else
        {
            reloadProblems = configurationManager.Reload();
        }

        var newConfig = new ConfigurationSnapshot(configurationManager.DaemonConfiguration, configurationManager.Applications.ToImmutableDictionary());

        var diff = BuildDiff(oldConfig, newConfig);
        var results = ApplyDiff(reloadProblems.Warnings, reloadProblems.Errors, diff, relodableServices);

        return TypedResults.Ok(results);
    }

    private static ConfigurationDiff BuildDiff(ConfigurationSnapshot oldConfig, ConfigurationSnapshot newConfig)
    {
        return new ConfigurationDiff
        {
            Added = [.. newConfig.Applications.Values.Where(@new => oldConfig.Applications.Values.Select(old => old.Name).All(oldName => oldName != @new.Name))],
            Removed = [.. oldConfig.Applications.Values.Where(old => newConfig.Applications.Values.Select(@new => @new.Name).All(newName => newName != old.Name))],
            Modified = [.. oldConfig.Applications.Values
                .Where(oldApp => newConfig.Applications.ContainsKey(oldApp.Name))
                .Select(oldApp => (Old: oldApp, New: newConfig.Applications[oldApp.Name]))
                .Where(t => t.Old != t.New)
            ],

            OldConfiguration = oldConfig.Daemon,
            NewConfiguration = newConfig.Daemon
        };
    }

    private static ReloadResult ApplyDiff(IEnumerable<string> warnings, IEnumerable<string> errors, ConfigurationDiff diff, IEnumerable<IConfigRelodable> relodableServices)
    {
        return relodableServices
            .Select(service => service.ReloadConfiguration(diff))
            .Aggregate(new ReloadResult([], warnings, errors), (acc, result) => new ReloadResult(
                Actions: acc.Actions.Concat(result.Actions),
                Warnings: acc.Warnings.Concat(result.Warnings),
                Errors: acc.Errors.Concat(result.Errors)
            ));
    }

    private sealed record ConfigurationSnapshot(DaemonConfiguration Daemon, ImmutableDictionary<string, ApplicationConfiguration> Applications);
}
