using EndpointMapper;
using Hexus.Daemon;
using Hexus.Configuration;
using Hexus.Daemon.Services;
using NReco.Logging.File;

const string reloadConfigOnChangeEnvVar = "ASPNETCORE_hostBuilder__reloadConfigOnChange";

// We need to always disable the reload config on change as
// 1. We don't use appsettings.json, however there is no way to disable this unless we use CreateEmptyBuilder
// 2. Since we usually don't spawn in the same directory as the DLLs but dirs such as the home directory, this causes lots of file watchers to be created
// We need to do this via environment variable as only envs are loaded before appsettings.json is loaded
Environment.SetEnvironmentVariable(reloadConfigOnChangeEnvVar, false.ToString());

var builder = WebApplication.CreateSlimBuilder(args);

Environment.SetEnvironmentVariable(reloadConfigOnChangeEnvVar, null);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    { "Logging:LogLevel:Default", Enum.GetName(LogLevel.Information) },
    { "Logging:LogLevel:Microsoft.AspNetCore", Enum.GetName(LogLevel.Warning) },
    { "Logging:LogLevel:Hexus.Daemon", Enum.GetName(builder.Environment.IsDevelopment() ? LogLevel.Trace : LogLevel.Information) },
});

builder.WebHost.UseKestrel((context, options) =>
{
    var config = options.ApplicationServices.GetRequiredService<HexusConfigurationManager>().DaemonConfiguration;

    // The socket could still exist, and if that is the case Kestrel will throw an exception
    if (Path.Exists(config.UnixSocket))
        File.Delete(config.UnixSocket);

    options.ListenUnixSocket(config.UnixSocket);

    if (config.HttpPort is { } httpPort and > 0)
        options.ListenLocalhost(httpPort);

    if (context.HostingEnvironment.IsDevelopment())
        options.ListenLocalhost(5104);
});

builder.Logging.AddFile(EnvironmentHelper.LogFile, x =>
{
    x.Append = true;
    x.UseUtcTimestamp = true;
});

// If we are running as a systemd service this will handle the Type=notify requirements
builder.Services.AddSystemd();
// If we are running as a Windows service this will handle the service lifecycle
builder.Services.AddWindowsService();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Clear();
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddProblemDetails();

builder.Services.AddSingleton<HexusConfigurationManager>();

builder.Services.AddSingleton<HexusLifecycle>();
builder.Services.AddSingleton<PerformanceTrackingService>();
builder.Services.AddSingleton<MemoryLimiterService>();

// We can't use AddHostedService here as we need to register these services under IConfigRelodable as well
builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<HexusLifecycle>());
builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<PerformanceTrackingService>());
builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<MemoryLimiterService>());

builder.Services.AddSingleton<StateManagerService>();
builder.Services.AddSingleton<ProcessStatisticsService>();
builder.Services.AddSingleton<ProcessLogsService>();
builder.Services.AddSingleton<ProcessManagerService>();

builder.Services.AddSingleton<IConfigRelodable>(sp => sp.GetRequiredService<ProcessManagerService>());
builder.Services.AddSingleton<IConfigRelodable>(sp => sp.GetRequiredService<HexusLifecycle>());
builder.Services.AddSingleton<IConfigRelodable>(sp => sp.GetRequiredService<MemoryLimiterService>());
builder.Services.AddSingleton<IConfigRelodable>(sp => sp.GetRequiredService<PerformanceTrackingService>());

var app = builder.Build();

var hexusConfiguration = app.Services.GetRequiredService<HexusConfigurationManager>();

foreach (var warning in hexusConfiguration.Warnings)
    app.Logger.LogWarning("A configuration warn was found: {warning}", warning);

foreach (var error in hexusConfiguration.Errors)
    app.Logger.LogError("A configuration error was found: {error}", error);

app.UseExceptionHandler();
app.MapEndpointMapperEndpoints();

app.Run();
