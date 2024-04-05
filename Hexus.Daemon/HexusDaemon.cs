using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Services;

namespace Hexus.Daemon;

internal static class HexusDaemon
{
    public static void StartDaemon(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        var isDevelopment = builder.Environment.IsDevelopment();

        var configurationManager = new HexusConfigurationManager(isDevelopment);

        AddAppSettings(builder.Configuration, configurationManager.AppSettings, isDevelopment);
        CleanSocketFile(configurationManager.Configuration);

        builder.WebHost.UseKestrel((context, options) =>
        {
            options.ListenUnixSocket(configurationManager.Configuration.UnixSocket);

            if (configurationManager.Configuration.HttpPort is { } httpPort and > 0)
                options.ListenLocalhost(httpPort);

            if (context.HostingEnvironment.IsDevelopment())
                options.ListenLocalhost(5104);
        });

        // If we are running as a systemd service this will handle the Type=notify requirements
        builder.Host.UseSystemd();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Clear();
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        builder.Services.AddProblemDetails();

        builder.Services.AddSingleton(configurationManager);
        builder.Services.AddTransient(sp => sp.GetRequiredService<HexusConfigurationManager>().Configuration);

        builder.Services.AddHostedService<HexusLifecycle>();
        builder.Services.AddHostedService<PerformanceTrackingService>();
        builder.Services.AddSingleton<ProcessManagerService>();

        var app = builder.Build();

        app.UseExceptionHandler();
        app.MapEndpointMapperEndpoints();

        app.Run();
    }

    private static void CleanSocketFile(HexusConfiguration configuration)
    {
        var name = Path.GetDirectoryName(configuration.UnixSocket)
                   ?? throw new InvalidOperationException("Cannot get the directory name for the unix socket");

        Directory.CreateDirectory(name);

        // On Windows .NET doesn't remove the socket, so it might be still there
        File.Delete(configuration.UnixSocket);
    }

    private static void AddAppSettings(
        ConfigurationManager configManager, Dictionary<object, object?>? appSettings, bool isDevelopment = false)
    {
        configManager.GetSection("Logging:LogLevel:Default").Value = LogLevel.Information.ToString();
        configManager.GetSection("Logging:LogLevel:Microsoft.AspNetCore").Value = LogLevel.Warning.ToString();

        if (isDevelopment)
            configManager.GetSection("Logging:LogLevel:Hexus.Daemon").Value = LogLevel.Trace.ToString();

        configManager.AddInMemoryCollection(HexusConfigurationManager.FlatDictionary(appSettings));
    }
}
