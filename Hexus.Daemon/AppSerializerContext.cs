// using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Contracts.Responses;
using Hexus.Daemon.Services;
using System.Text.Json.Serialization;
// using YamlDotNet.Serialization;

namespace Hexus.Daemon;

[JsonSerializable(typeof(HttpValidationProblemDetails))]
[JsonSerializable(typeof(ApplicationResponse))]
[JsonSerializable(typeof(ApplicationLog))]
[JsonSerializable(typeof(IEnumerable<ApplicationResponse>))]
[JsonSerializable(typeof(IAsyncEnumerable<ApplicationLog>))]
// [JsonSerializable(typeof(NewApplicationRequest))]
// [JsonSerializable(typeof(EditApplicationRequest))]
[JsonSerializable(typeof(SendInputRequest))]
[JsonSerializable(typeof(GenericFailureResponse))]
[JsonSerializable(typeof(ReloadResult))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
public partial class AppJsonSerializerContext : JsonSerializerContext;

// [YamlSerializable(typeof(HexusConfigurationFile))]
// [YamlSerializable(typeof(HexusApplication))]
// [YamlStaticContext]
// public partial class AppYamlSerializerContext;
