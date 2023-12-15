using Hexus.Daemon.Contracts;
using System.Text.Json.Serialization;

namespace Hexus.Daemon;

[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(HexusApplicationResponse))]
[JsonSerializable(typeof(IEnumerable<HexusApplicationResponse>))]
[JsonSerializable(typeof(IAsyncEnumerable<string>))]
[JsonSerializable(typeof(NewApplicationRequest))]
[JsonSerializable(typeof(EditApplicationRequest))]
[JsonSerializable(typeof(SendInputRequest))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
internal partial class AppJsonSerializerContext : JsonSerializerContext;
