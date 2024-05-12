using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Services;

namespace Hexus.Daemon.Extensions;

internal static class MapperExtensions
{
    public static HexusApplication MapToApplication(this NewApplicationRequest request) =>
        new()
        {
            Name = request.Name,
            Executable = EnvironmentHelper.NormalizePath(request.Executable),
            Arguments = request.Arguments,
            WorkingDirectory = EnvironmentHelper.NormalizePath(request.WorkingDirectory ?? EnvironmentHelper.Home),
            Note = request.Note,
            EnvironmentVariables = request.EnvironmentVariables ?? [],
        };

    public static ApplicationResponse MapToResponse(this HexusApplication application, ApplicationStatistics applicationStatisticsResponse) =>
        new(
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

    public static IEnumerable<ApplicationResponse> MapToResponse(this IEnumerable<HexusApplication> applications,
        Func<HexusApplication, ApplicationStatistics> getApplicationStats)
    {
        return applications.Select(app => app.MapToResponse(getApplicationStats(app)));
    }
}
