using EndpointMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hexus.Daemon.Endpoints;

public sealed class UpdateConfigurationEndpoint : IEndpoint
{
    [HttpMapPost("/")]
    public NoContent Handle(UpdateConfiguration body, IOptions<HexusConfiguration> options)
    {
        options.Value.Test = body.Test;

        SaveConfig("config.yml", options.Value);

        return TypedResults.NoContent();
    }

    // TODO: move it to access the path without passing it, caching the serializer
    private static void SaveConfig(string Path, HexusConfiguration options)
    {
        var serializer = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var yml = serializer.Serialize(options);

        File.WriteAllText(Path, yml);
    }

    public sealed record UpdateConfiguration(string Test);
}
