using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json.Serialization;

namespace Hexus.Daemon;

internal class HexusDaemon
{
    public static void StartDaemon(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        var configurationManager = new HexusConfigurationManager(builder.Environment.IsDevelopment());

        builder.WebHost.UseKestrel((context, options) =>
        {
            var name = Path.GetDirectoryName(configurationManager.Configuration.UnixSocket)
                       ?? throw new InvalidOperationException("Cannot get the directory name for the unix socket");

            Directory.CreateDirectory(name);

            // On Windows .NET doesn't remove the socket, so it might be still there
            File.Delete(configurationManager.Configuration.UnixSocket);

            options.ListenUnixSocket(configurationManager.Configuration.UnixSocket);

            if (configurationManager.Configuration.HttpPort is not -1)
                options.ListenLocalhost(configurationManager.Configuration.HttpPort);

            if (context.HostingEnvironment.IsDevelopment())
                options.ListenLocalhost(5104);
        });

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        builder.Services.AddSingleton(configurationManager);
        builder.Services.AddTransient(sp => sp.GetRequiredService<HexusConfigurationManager>().Configuration);

        builder.Services.AddHostedService<HexusLifecycle>();
        builder.Services.AddSingleton<ProcessManagerService>();

        var app = builder.Build();
        
        app.MapEndpointMapperEndpoints();

        app.Run();
    }
}

[JsonSerializable(typeof(IEnumerable<HexusApplicationResponse>))]
[JsonSerializable(typeof(HexusApplicationResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
internal partial class AppJsonSerializerContext : JsonSerializerContext;
