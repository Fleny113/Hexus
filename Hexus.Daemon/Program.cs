using EndpointMapper;
// using FluentValidation;
using Hexus.Daemon;
using Hexus.Configuration;
// using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Services;
// using Hexus.Daemon.Validators;
using NReco.Logging.File;

const string reloadConfigOnChangeEnvVar = "ASPNETCORE_hostBuilder__reloadConfigOnChange";

// This has to be done before the call to CreateSlimBuilder, otherwise it will configure appsettings.json to reload on file change
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

// Validators
// builder.Services.AddScoped<IValidator<EditApplicationRequest>, EditApplicationValidator>();
// builder.Services.AddScoped<IValidator<NewApplicationRequest>, NewApplicationValidator>();
// builder.Services.AddScoped<IValidator<SendInputRequest>, SendInputValidator>();

// Configuration
builder.Services.AddSingleton<HexusConfigurationManager>();
// builder.Services.AddTransient(sp => sp.GetRequiredService<HexusConfigurationManager>().DaemonConfiguration);

// Services & HostedServices
builder.Services.AddSingleton<HexusLifecycle>();
builder.Services.AddSingleton<PerformanceTrackingService>();
builder.Services.AddSingleton<MemoryLimiterService>();

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

// TODO: Expose these warnings in an endpoint, and call it from the CLI to show them to the user
var hexusConfiguration = app.Services.GetRequiredService<HexusConfigurationManager>();

foreach (var warning in hexusConfiguration.Warnings)
{
    app.Logger.LogWarning("A configuration warn was found: {warning}", warning);
}

foreach (var error in hexusConfiguration.Errors)
{
    app.Logger.LogError("A configuration error was found: {error}", error);
}

app.UseExceptionHandler();
app.MapEndpointMapperEndpoints();

app.Run();
