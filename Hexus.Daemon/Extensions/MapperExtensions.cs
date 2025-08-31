using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Services;

namespace Hexus.Daemon.Extensions;

internal static class MapperExtensions
{
    public static HexusApplication MapToApplication(this NewApplicationRequest request)
    {
        return new HexusApplication
        {
            Name = request.Name,
            Executable = Path.GetFullPath(request.Executable),
            Arguments = request.Arguments,
            WorkingDirectory = Path.GetFullPath(request.WorkingDirectory ?? EnvironmentHelper.Home),
            Note = request.Note,
            EnvironmentVariables = request.EnvironmentVariables ?? [],
            MemoryLimit = request.MemoryLimit,
        };
    }

    public static ApplicationResponse MapToResponse(this HexusApplication application, ApplicationStatistics applicationStatisticsResponse)
    {
        return new ApplicationResponse(
            Name: application.Name,
            Executable: Path.GetFullPath(application.Executable),
            Arguments: application.Arguments,
            WorkingDirectory: Path.GetFullPath(application.WorkingDirectory),
            Note: application.Note,
            EnvironmentVariables: application.EnvironmentVariables,
            Status: application.Status,
            ProcessUptime: applicationStatisticsResponse.ProcessUptime,
            ProcessId: applicationStatisticsResponse.ProcessId,
            CpuUsage: applicationStatisticsResponse.CpuUsage,
            MemoryUsage: applicationStatisticsResponse.MemoryUsage,
            MemoryLimit: application.MemoryLimit
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
            EnvironmentVariables = x.Value.EnvironmentVariables,
            MemoryLimit = x.Value.MemoryLimit,
        }));

        var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var defaultMemoryLimit = (ulong)(totalMemory * 0.25);

        return new HexusConfiguration
        {
            UnixSocket = configurationFile.UnixSocket ?? EnvironmentHelper.SocketFile,
            HttpPort = configurationFile.HttpPort,
            CpuRefreshIntervalSeconds = configurationFile.CpuRefreshIntervalSeconds ?? 2.5,
            MemoryLimitCheckIntervalSeconds = configurationFile.MemoryLimitCheckIntervalSeconds ?? 10.0,
            MemoryLimit = configurationFile.MemoryLimit ?? defaultMemoryLimit,
            Applications = applications?.ToDictionary() ?? [],
        };
    }
    public static HexusConfigurationFile MapToConfigFile(this HexusConfiguration configuration)
    {
        // If we are using default values, we can omit writing them to the file
        var socket = configuration.UnixSocket != EnvironmentHelper.SocketFile ? configuration.UnixSocket : null;
        var cpuRefresh = Math.Abs(configuration.CpuRefreshIntervalSeconds - 2.5) > 0.1 ? configuration.CpuRefreshIntervalSeconds : (double?)null;
        var memRefresh = Math.Abs(configuration.MemoryLimitCheckIntervalSeconds - 10.0) > 0.1 ? configuration.MemoryLimitCheckIntervalSeconds : (double?)null;

        var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var defaultMemoryLimit = (ulong)(totalMemory * 0.25);

        var memoryLimit = configuration.MemoryLimit != defaultMemoryLimit ? configuration.MemoryLimit : (ulong?)null;

        var applications = configuration.Applications.Select(x => new KeyValuePair<string, HexusApplication>(x.Key, new HexusApplication
        {
            // We don't want to serialize the name in the config file
            Name = null!,
            Executable = x.Value.Executable,
            Arguments = x.Value.Arguments,
            WorkingDirectory = x.Value.WorkingDirectory,
            Status = x.Value.Status,
            Note = x.Value.Note,
            EnvironmentVariables = x.Value.EnvironmentVariables,
            MemoryLimit = x.Value.MemoryLimit,
        }));

        return new HexusConfigurationFile
        {
            UnixSocket = socket,
            HttpPort = configuration.HttpPort,
            CpuRefreshIntervalSeconds = cpuRefresh,
            MemoryLimitCheckIntervalSeconds = memRefresh,
            MemoryLimit = memoryLimit,
            Applications = applications.ToDictionary(),
        };
    }

    public static string MapToErrorString(this ProcessManagerService.SpawnProcessError error)
    {
        return error switch
        {
            ProcessManagerService.SpawnProcessError.ExitEarly => "The application exited early.",
            ProcessManagerService.SpawnProcessError.NotFound => "The application executable was not found.",
            ProcessManagerService.SpawnProcessError.PermissionDenied => "Permission denied while starting the application.",
            ProcessManagerService.SpawnProcessError.InvalidExecutable => "The application executable is invalid.",
            ProcessManagerService.SpawnProcessError.CommandTooLong => "The command line was too long.",
            ProcessManagerService.SpawnProcessError.Unknown => "An unknown error occurred while starting the application.",
            _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
        };
    }
}
