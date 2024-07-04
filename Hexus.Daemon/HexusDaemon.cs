using EndpointMapper;
using FluentValidation;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Services;
using Hexus.Daemon.Validators;
using NReco.Logging.File;

namespace Hexus.Daemon;

internal static class HexusDaemon
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        var configurationManager = new HexusConfigurationManager();

        AddAppSettings(builder.Configuration, builder.Environment.IsDevelopment());

        // The socket might not get removed, so it might be still there, but Kestrel will throw an exception if the file already exists
        // We need to check that the directory where the socket is exists before deleting it or File.Delete will throw an exception
        var dirname = Path.GetDirectoryName(configurationManager.Configuration.UnixSocket);
        if (Directory.Exists(dirname))
        {
            File.Delete(configurationManager.Configuration.UnixSocket);
        }

        builder.WebHost.UseKestrel((context, options) =>
        {
            options.ListenUnixSocket(configurationManager.Configuration.UnixSocket);

            if (configurationManager.Configuration.HttpPort is { } httpPort and > 0)
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
        builder.Services.AddSingleton(configurationManager);
        builder.Services.AddTransient(sp => sp.GetRequiredService<HexusConfigurationManager>().Configuration);

        // Services & HostedServices
        builder.Services.AddHostedService<HexusLifecycle>();
        builder.Services.AddHostedService<PerformanceTrackingService>();

        builder.Services.AddSingleton<ProcessStatisticsService>();
        builder.Services.AddSingleton<ProcessLogsService>();
        builder.Services.AddSingleton<ProcessManagerService>();

        var app = builder.Build();

        // We only want to print this message if:
        //  - We are not on Windows, it is standard that XDG_RUNTIME_DIR does not exist on Windows
        //  - XDG_RUNTIME_DIR is not set
        //  - The user hasn't specified another location for the socket (so we are still using the default location)
        if (!OperatingSystem.IsWindows() && EnvironmentHelper.XdgRuntime is null && configurationManager.Configuration.UnixSocket == EnvironmentHelper.ConfigurationFile)
        {
            app.Logger.LogWarning("The XDG_RUNTIME_DIR environment is missing. Defaulting to {socket}", configurationManager.Configuration.UnixSocket);
        }

        app.UseExceptionHandler();
        app.MapEndpointMapperEndpoints();

        app.Run();
    }

    private static void AddAppSettings(ConfigurationManager configManager, bool isDevelopment = false)
    {
        configManager.GetSection("Logging:LogLevel:Default").Value = LogLevel.Information.ToString();
        configManager.GetSection("Logging:LogLevel:Microsoft.AspNetCore").Value = LogLevel.Warning.ToString();

        if (isDevelopment)
            configManager.GetSection("Logging:LogLevel:Hexus.Daemon").Value = LogLevel.Trace.ToString();
    }
}
