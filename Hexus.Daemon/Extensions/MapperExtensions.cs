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
            WorkingDirectory = EnvironmentHelper.NormalizePath(request.WorkingDirectory),
            Note = request.Note,
            EnvironmentVariables = request.EnvironmentVariables,
        };

    public static HexusApplicationResponse MapToResponse(this HexusApplication application) =>
        new(
            application.Name,
            EnvironmentHelper.NormalizePath(application.Executable),
            application.Arguments,
            Note: application.Note,
            WorkingDirectory: EnvironmentHelper.NormalizePath(application.WorkingDirectory),
            EnvironmentVariables: application.EnvironmentVariables,
            Status: application.Status,
            ProcessUptime: application.Process is { HasExited: false } ? DateTime.Now - application.Process.StartTime : TimeSpan.Zero,
            ProcessId: application.Process is { HasExited: false } ? application.Process.Id : 0,
            CpuUsage: application.LastCpuUsage,
            MemoryUsage: PerformanceTrackingService.GetMemoryUsage(application)
        );

    public static IEnumerable<HexusApplicationResponse> MapToResponse(this Dictionary<string, HexusApplication> applications) =>
        applications
            .Select(pair => pair.Value.MapToResponse());
}
