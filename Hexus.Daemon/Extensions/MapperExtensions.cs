using Hexus.Configuration;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Services;

namespace Hexus.Daemon.Extensions;

public static class MapperExtensions
{
    extension(ApplicationConfiguration application)
    {
        public ApplicationResponse MapToResponse(ApplicationStatistics applicationStatisticsResponse)
        {
            return new ApplicationResponse(
                Name: application.Name,
                Executable: application.Executable,
                Arguments: application.Arguments,
                WorkingDirectory: application.WorkingDirectory,
                Enabled: application.Enabled,
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
    }

    extension(IEnumerable<ApplicationConfiguration> applications)
    {
        internal IEnumerable<ApplicationResponse> MapToResponse(Func<ApplicationConfiguration, ApplicationStatistics> getApplicationStats)
        {
            return applications.Select(app => app.MapToResponse(getApplicationStats(app)));
        }
    }

    extension(ProcessManagerService.SpawnProcessError error)
    {
        internal string MapToErrorString()
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
}
