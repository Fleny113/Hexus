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

builder.Services.AddEndpointMapper<Program>();

var app = builder.Build();

app.UseEndpointMapper();

app.Run();
