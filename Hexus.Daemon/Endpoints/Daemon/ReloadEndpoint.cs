using EndpointMapper;
using Hexus.Configuration;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Hexus.Daemon.Endpoints.Daemon;

internal class ReloadEndpoint : IEndpoint, IRegisterEndpoint
{
    public static void Register(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/daemon/reload", Handle);
    }

    public static Ok<ReloadResult> Handle(
        [FromServices] HexusConfigurationManager configurationManager,
        [FromServices] IEnumerable<IConfigRelodable> relodableServices
    )
    {
        var oldConfig = (configurationManager.DaemonConfiguration, configurationManager.Applications);

        configurationManager.Reload();

        var newConfig = (configurationManager.DaemonConfiguration, configurationManager.Applications);

        var diff = new ConfigurationDiff
        {
            Added = [.. newConfig.Applications.Values.Where(@new => oldConfig.Applications.Values.Select(old => old.Name).All(oldName => oldName != @new.Name))],
            Removed = [.. oldConfig.Applications.Values.Where(old => newConfig.Applications.Values.Select(@new => @new.Name).All(newName => newName != old.Name))],
            Modified = [.. oldConfig.Applications.Values
                .Where(oldApp => newConfig.Applications.ContainsKey(oldApp.Name))
                .Select(oldApp => (Old: oldApp, New: newConfig.Applications[oldApp.Name]))
                .Where(t => t.Old != t.New)
            ],

            OldConfiguration = oldConfig.DaemonConfiguration,
            NewConfiguration = newConfig.DaemonConfiguration
        };

        var results = relodableServices.Select(service => service.ReloadConfiguration(diff)).Aggregate(new ReloadResult([], [], []), (acc, result) => new ReloadResult(
            Actions: acc.Actions.Concat(result.Actions),
            Warnings: acc.Warnings.Concat(result.Warnings),
            Errors: acc.Errors.Concat(result.Errors)
        ));

        return TypedResults.Ok(results);
    }
}
