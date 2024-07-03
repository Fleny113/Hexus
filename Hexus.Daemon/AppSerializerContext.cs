using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Contracts.Responses;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace Hexus.Daemon;

[JsonSerializable(typeof(HttpValidationProblemDetails))]
[JsonSerializable(typeof(ApplicationResponse))]
[JsonSerializable(typeof(IEnumerable<ApplicationResponse>))]
[JsonSerializable(typeof(IAsyncEnumerable<ApplicationLog>))]
[JsonSerializable(typeof(NewApplicationRequest))]
[JsonSerializable(typeof(EditApplicationRequest))]
[JsonSerializable(typeof(SendInputRequest))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
internal partial class AppJsonSerializerContext : JsonSerializerContext;

[YamlSerializable(typeof(HexusConfigurationFile))]
[YamlSerializable(typeof(HexusApplication))]
[YamlStaticContext]
public partial class AppYamlSerializerContext : StaticContext;
