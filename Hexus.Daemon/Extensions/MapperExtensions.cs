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
        };
    }

    public static HexusApplicationResponse MapToResponse(this HexusApplication application)
    {
        return new HexusApplicationResponse(
            Name: application.Name, 
            Executable: EnvironmentHelper.NormalizePath(application.Executable), 
            Arguments: application.Arguments,
            WorkingDirectory: EnvironmentHelper.NormalizePath(application.WorkingDirectory), 
            Status: application.Status, 
            CpuUsage: application.LastCpuUsage, 
            MemoryUsage: ProcessManagerService.GetMemoryUsage(application)
        );
    }

    public static IEnumerable<HexusApplicationResponse> MapToResponse(this Dictionary<string, HexusApplication> applications)
    {
        return applications
            .Select(pair => pair.Value.MapToResponse());
    }
}
