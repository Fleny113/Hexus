using Hexus.Daemon.Contracts;
using System.Text.Json.Serialization;

namespace Hexus.Daemon;

[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(NewApplicationRequest))]
[JsonSerializable(typeof(EditApplicationRequest))]
[JsonSerializable(typeof(IEnumerable<HexusApplicationResponse>))]
[JsonSerializable(typeof(HexusApplicationResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
internal partial class AppJsonSerializerContext : JsonSerializerContext;
