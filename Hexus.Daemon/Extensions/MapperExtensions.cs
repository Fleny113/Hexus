using Hexus.Configuration;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Services;

namespace Hexus.Daemon.Extensions;

internal static class MapperExtensions
{
    public static ApplicationResponse MapToResponse(this ApplicationConfiguration application, ApplicationStatistics applicationStatisticsResponse)
    {
        return new ApplicationResponse(
            Name: application.Name,
            Executable: Path.GetFullPath(application.Executable),
            Arguments: application.Arguments,
            WorkingDirectory: Path.GetFullPath(application.WorkingDirectory),
            Note: application.Note,
            EnvironmentVariables: application.EnvironmentVariables,
            Status: applicationStatisticsResponse.Status,
            ProcessUptime: applicationStatisticsResponse.ProcessUptime,
            ProcessId: applicationStatisticsResponse.ProcessId,
            CpuUsage: applicationStatisticsResponse.CpuUsage,
            MemoryUsage: applicationStatisticsResponse.MemoryUsage,
            MemoryLimit: application.MemoryLimit
        );
    }

    public static IEnumerable<ApplicationResponse> MapToResponse(this IEnumerable<ApplicationConfiguration> applications,
        Func<ApplicationConfiguration, ApplicationStatistics> getApplicationStats)
    {
        return applications.Select(app => app.MapToResponse(getApplicationStats(app)));
    }

    public static string MapToErrorString(this ProcessManagerService.SpawnProcessError error)
    {
        return error switch
        {
            ProcessManagerService.SpawnProcessError.ExitEarly => "The application exited early.",
            ProcessManagerService.SpawnProcessError.NotFound => "The application executable was not found.",
            ProcessManagerService.SpawnProcessError.PermissionDenied => "Permission denied while starting the application.",
            ProcessManagerService.SpawnProcessError.InvalidExecutable => "The application executable is invalid.",
            ProcessManagerService.SpawnProcessError.CommandTooLong => "The command was too long.",
            ProcessManagerService.SpawnProcessError.Unknown => "An unknown error occurred while starting the application.",
            _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
        };
    }
}
