﻿using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;

namespace Hexus.Daemon.Extensions;

internal static class MapperExtensions
{
    public static HexusApplication MapToApplication(this NewApplicationRequest request)
    {
        return new HexusApplication
        {
            Name = request.Name,
            Executable = EnvironmentHelper.NormalizePath(request.Executable),
            Arguments = request.Arguments,
            WorkingDirectory = EnvironmentHelper.NormalizePath(request.WorkingDirectory),
            Note = request.Note,
            EnvironmentVariables = request.EnvironmentVariables,
        };
    }

    public static HexusApplicationResponse MapToResponse(this HexusApplication application)
    {
        return new HexusApplicationResponse(
            Name: application.Name,
            Executable: EnvironmentHelper.NormalizePath(application.Executable),
            Arguments: application.Arguments,
            Note: application.Note,
            WorkingDirectory: EnvironmentHelper.NormalizePath(application.WorkingDirectory),
            EnvironmentVariables: application.EnvironmentVariables,
            Status: application.Status,
            ProcessUptime: application.Process is { HasExited: false } ? DateTime.Now - application.Process.StartTime : TimeSpan.Zero,
            ProcessId: application.Process is { HasExited: false } ? application.Process.Id : 0,
            CpuUsage: application.LastCpuUsage,
            MemoryUsage: PerformaceTrackingService.GetMemoryUsage(application)
        );
    }

    public static IEnumerable<HexusApplicationResponse> MapToResponse(this Dictionary<string, HexusApplication> applications)
    {
        return applications
            .Select(pair => pair.Value.MapToResponse());
    }
}
