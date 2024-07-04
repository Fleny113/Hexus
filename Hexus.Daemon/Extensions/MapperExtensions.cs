using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Services;

namespace Hexus.Daemon.Extensions;

internal static class MapperExtensions
{
    public static HexusApplication MapToApplication(this NewApplicationRequest request)
    {
        return new()
        {
            Name = request.Name,
            Executable = EnvironmentHelper.NormalizePath(request.Executable),
            Arguments = request.Arguments,
            WorkingDirectory = EnvironmentHelper.NormalizePath(request.WorkingDirectory ?? EnvironmentHelper.Home),
            Note = request.Note,
            EnvironmentVariables = request.EnvironmentVariables ?? [],
        };
    }

    public static ApplicationResponse MapToResponse(this HexusApplication application, ApplicationStatistics applicationStatisticsResponse)
    {
        return new(
            Name: application.Name,
            Executable: EnvironmentHelper.NormalizePath(application.Executable),
            Arguments: application.Arguments,
            WorkingDirectory: EnvironmentHelper.NormalizePath(application.WorkingDirectory),
            Note: application.Note,
            EnvironmentVariables: application.EnvironmentVariables,
            Status: application.Status,
            ProcessUptime: applicationStatisticsResponse.ProcessUptime,
            ProcessId: applicationStatisticsResponse.ProcessId,
            CpuUsage: applicationStatisticsResponse.CpuUsage,
            MemoryUsage: applicationStatisticsResponse.MemoryUsage
        );
    }
        

    public static IEnumerable<ApplicationResponse> MapToResponse(this IEnumerable<HexusApplication> applications, 
        Func<HexusApplication, ApplicationStatistics> getApplicationStats)
    {
        return applications.Select(app => app.MapToResponse(getApplicationStats(app)));
    }

    public static HexusConfiguration MapToConfig(this HexusConfigurationFile configurationFile)
    {
        var applications = configurationFile.Applications?.Select(x => new KeyValuePair<string, HexusApplication>(x.Key, new()
        {
            Name = x.Key,
            Executable = x.Value.Executable,
            Arguments = x.Value.Arguments,
            WorkingDirectory = x.Value.WorkingDirectory,
            Status = x.Value.Status,
            Note = x.Value.Note,
            EnvironmentVariables = x.Value.EnvironmentVariables
        }));

        return new()
        {
            UnixSocket = configurationFile.UnixSocket ?? EnvironmentHelper.SocketFile,
            HttpPort = configurationFile.HttpPort,
            CpuRefreshIntervalSeconds = configurationFile.CpuRefreshIntervalSeconds ?? 2.5,
            Applications = applications?.ToDictionary() ?? [],
        };
    }
    public static HexusConfigurationFile MapToConfigFile(this HexusConfiguration configuration)
    {
        // If we are using default values, we can omit writing them to the file
        var socket = configuration.UnixSocket != EnvironmentHelper.SocketFile ? configuration.UnixSocket : null;
        var cpuRefresh = configuration.CpuRefreshIntervalSeconds != 2.5 ? configuration.CpuRefreshIntervalSeconds : (double?)null;

        var applications = configuration.Applications.Select(x => new KeyValuePair<string, HexusApplication>(x.Key, new()
        {
            // We don't want to serialize the name in the config file
            Name = null!,
            Executable = x.Value.Executable,
            Arguments = x.Value.Arguments,
            WorkingDirectory = x.Value.WorkingDirectory,
            Status = x.Value.Status,
            Note = x.Value.Note,
            EnvironmentVariables = x.Value.EnvironmentVariables
        }));

        return new()
        {
            UnixSocket = socket,
            HttpPort = configuration.HttpPort,
            CpuRefreshIntervalSeconds = cpuRefresh,
            Applications = applications.ToDictionary(),
        };
    }
}
