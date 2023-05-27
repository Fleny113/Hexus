using EndpointMapper;
using Hexus.Daemon;

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

builder.WebHost.UseKestrel(options =>
{
    var unixSocket = builder.Configuration.GetValue<string?>($"{HexusConfiguration.ConfigurationSection}:{nameof(HexusConfiguration.UnixSocket)}") 
        ?? $"{HexusConfiguration.HexusHomeFolder}/hexus.sock";
    var httpPort = builder.Configuration.GetValue<int?>($"{HexusConfiguration.ConfigurationSection}:{nameof(HexusConfiguration.HttpPort)}") 
        ?? -1;
    var localhost = builder.Configuration.GetValue<bool?>($"{HexusConfiguration.ConfigurationSection}:{nameof(HexusConfiguration.Localhost)}") 
        ?? true;

    if (unixSocket is not (null or "none"))
    {
        // On windows .NET doesn't remove the socket
        File.Delete(unixSocket);

        options.ListenUnixSocket(unixSocket);
    }

    if (httpPort is not -1)
    {
        if (localhost)
            options.ListenLocalhost(httpPort);
        else
            options.ListenAnyIP(httpPort);
    }
});

builder.Services.AddEndpointMapper<Program>();

var app = builder.Build();

app.UseEndpointMapper();

app.Run();
