﻿using Hexus.Daemon.Configuration;

namespace Hexus;

internal static class Configuration
{
    public static HexusConfigurationManager HexusConfigurationManager { get; } = new(EnvironmentHelper.IsDevelopment);
    public static HexusConfiguration HexusConfiguration => HexusConfigurationManager.Configuration;
}
