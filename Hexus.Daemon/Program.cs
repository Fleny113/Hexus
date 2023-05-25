using EndpointMapper;
using Hexus.Daemon;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration.Add<YamlConfigurationSource>(s =>
{
    s.Path = "config.yml";
    s.Optional = true;
    s.SectionRoot = HexusConfiguration.ConfigurationSection;
});

builder.Services.AddOptions<HexusConfiguration>()
    .BindConfiguration(HexusConfiguration.ConfigurationSection);

builder.Services.AddEndpointMapper<Program>();

var app = builder.Build();

app.UseEndpointMapper();

app.Run();
