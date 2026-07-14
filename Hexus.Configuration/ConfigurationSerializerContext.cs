using System.Text.Json.Serialization;
using Tomlyn.Serialization;

namespace Hexus.Configuration;

[TomlSerializable(typeof(DaemonConfiguration.DaemonConfigurationRaw))]
[TomlSerializable(typeof(ApplicationConfiguration.ApplicationConfigurationRaw))]
[TomlSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.KebabCaseLower)]
internal partial class ConfigurationSerializerContext : TomlSerializerContext { }
