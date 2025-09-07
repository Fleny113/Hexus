using EndpointMapper;
using FluentValidation;
using Hexus.Daemon;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Services;
using Hexus.Daemon.Validators;
using NReco.Logging.File;

const string reloadConfigOnChangeEnvVar = "ASPNETCORE_hostBuilder__reloadConfigOnChange";

// This has to be done before the call to CreateSlimBuilder, otherwise it will configure appsettings.json to reload on file change
Environment.SetEnvironmentVariable(reloadConfigOnChangeEnvVar, false.ToString());

var builder = WebApplication.CreateSlimBuilder(args);

Environment.SetEnvironmentVariable(reloadConfigOnChangeEnvVar, null);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    {"Logging:LogLevel:Default", Enum.GetName(LogLevel.Information) },
    {"Logging:LogLevel:Microsoft.AspNetCore", Enum.GetName(LogLevel.Warning) },
    {"Logging:LogLevel:Hexus.Daemon", Enum.GetName(builder.Environment.IsDevelopment() ? LogLevel.Trace : LogLevel.Information) },
});

builder.WebHost.UseKestrel((context, options) =>
{
    var config = options.ApplicationServices.GetRequiredService<HexusConfiguration>();

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

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Clear();
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddProblemDetails();

// Validators
builder.Services.AddScoped<IValidator<EditApplicationRequest>, EditApplicationValidator>();
builder.Services.AddScoped<IValidator<NewApplicationRequest>, NewApplicationValidator>();
builder.Services.AddScoped<IValidator<SendInputRequest>, SendInputValidator>();

// Configuration
builder.Services.AddSingleton<HexusConfigurationManager>();
builder.Services.AddTransient<HexusConfiguration>(sp => sp.GetRequiredService<HexusConfigurationManager>().Configuration);

// Services & HostedServices
builder.Services.AddHostedService<HexusLifecycle>();
builder.Services.AddHostedService<PerformanceTrackingService>();
builder.Services.AddHostedService<MemoryLimiterService>();

builder.Services.AddSingleton<ProcessStatisticsService>();
builder.Services.AddSingleton<ProcessLogsService>();
builder.Services.AddSingleton<ProcessManagerService>();

var app = builder.Build();

var hexusConfiguration = app.Services.GetRequiredService<HexusConfiguration>();

// We only want to print this message if:
//  - We are not on Windows, it is standard that XDG_RUNTIME_DIR does not exist on Windows
//  - XDG_RUNTIME_DIR is not set
//  - The user hasn't specified another location for the socket (so we are still using the default location)
if (!OperatingSystem.IsWindows() && EnvironmentHelper.XdgRuntime is null && hexusConfiguration.UnixSocket == EnvironmentHelper.SocketFile)
{
    app.Logger.LogWarning("The XDG_RUNTIME_DIR environment is missing. Defaulting socket location to {socket}", hexusConfiguration.UnixSocket);
}

app.UseExceptionHandler();
app.MapEndpointMapperEndpoints();

app.Run();
