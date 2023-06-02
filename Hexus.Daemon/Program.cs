using EndpointMapper;
using FluentValidation;
using Hexus.Daemon;
using Hexus.Daemon.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration.Add<YamlConfigurationSource>(s =>
{
    s.SectionRoot = HexusConfiguration.ConfigurationSection;
    s.Path = HexusConfiguration.ConfigurationFilePath;
    s.Optional = true;

    s.ResolveFileProvider();
});

builder.Services.AddOptions<HexusConfiguration>()
    .BindConfiguration(HexusConfiguration.ConfigurationSection);

builder.WebHost.UseKestrel((context, options) =>
{
    var config = new HexusConfiguration();

    context.Configuration.Bind(HexusConfiguration.ConfigurationSection, config);

    if (config.UnixSocket is not (null or "none"))
    {
        // On windows .NET doesn't remove the socket
        File.Delete(config.UnixSocket);

        options.ListenUnixSocket(config.UnixSocket);
    }

    if (config.HttpPort is not -1)
    {
        if (config.Localhost)
            options.ListenLocalhost(config.HttpPort);
        else
            options.ListenAnyIP(config.HttpPort);
    }

    if (context.HostingEnvironment.IsDevelopment())
    {
        options.ListenLocalhost(5104);
    }
});

builder.Services.AddEndpointMapper<Program>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddSingleton<ProcessManagerService>();

var app = builder.Build();

var pmService = app.Services.GetRequiredService<ProcessManagerService>();

app.Lifetime.ApplicationStarted.Register(pmService.ApplicationStartup);
app.Lifetime.ApplicationStopped.Register(pmService.ApplicationShutdown);

app.UseEndpointMapper();

app.Run();
