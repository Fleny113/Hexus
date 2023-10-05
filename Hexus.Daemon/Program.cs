using EndpointMapper;
using FluentValidation;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;

var builder = WebApplication.CreateSlimBuilder(args);

var configurationManager = new HexusConfigurationManager();

builder.Services.AddSingleton(configurationManager);
builder.Services.AddTransient(sp => sp.GetRequiredService<HexusConfigurationManager>().Configuration);

builder.WebHost.UseKestrel((context, options) =>
{
    if (configurationManager.Configuration.UnixSocket is not (null or "none"))
    {
        var directory = Path.GetDirectoryName(configurationManager.Configuration.UnixSocket) 
            ?? throw new Exception("Unable to fetch the directory name for the UNIX socket file location");

        Directory.CreateDirectory(directory);

        // On windows .NET doesn't remove the socket
        File.Delete(configurationManager.Configuration.UnixSocket);

        options.ListenUnixSocket(configurationManager.Configuration.UnixSocket);
    }

    if (configurationManager.Configuration.HttpPort is not -1)
    {
        options.ListenLocalhost(configurationManager.Configuration.HttpPort);
    }

    if (context.HostingEnvironment.IsDevelopment())
    {
        options.ListenLocalhost(5104);
    }
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddSingleton<ProcessManagerService>();

var app = builder.Build();

var pmService = app.Services.GetRequiredService<ProcessManagerService>();

app.Lifetime.ApplicationStarted.Register(pmService.ApplicationStartup);
app.Lifetime.ApplicationStopped.Register(pmService.ApplicationShutdown);

app.MapEndpointMapperEndpoints();

app.Run();
