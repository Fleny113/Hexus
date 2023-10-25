using EndpointMapper;
using FluentValidation;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

var configurationManager = new HexusConfigurationManager(builder.Environment.IsDevelopment());

builder.WebHost.UseKestrel((context, options) =>
{
    if (configurationManager.Configuration.UnixSocket is not null)
    {
        var directory = Path.GetDirectoryName(configurationManager.Configuration.UnixSocket)
                        ?? throw new Exception("Unable to fetch the directory name for the UNIX socket file location");

        Directory.CreateDirectory(directory);

        // On windows .NET doesn't remove the socket
        File.Delete(configurationManager.Configuration.UnixSocket);

        options.ListenUnixSocket(configurationManager.Configuration.UnixSocket);
    }

    if (configurationManager.Configuration.HttpPort is not -1)
        options.ListenLocalhost(configurationManager.Configuration.HttpPort);

    if (context.HostingEnvironment.IsDevelopment())
        options.ListenLocalhost(5104);
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<HexusApplicationStatus>());
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddSingleton(configurationManager);
builder.Services.AddTransient(sp => sp.GetRequiredService<HexusConfigurationManager>().Configuration);

builder.Services.AddHostedService<HexusLifecycle>();
builder.Services.AddSingleton<ProcessManagerService>();

var app = builder.Build();

app.MapEndpointMapperEndpoints();

app.Run();

[JsonSerializable(typeof(HexusApplication))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;
