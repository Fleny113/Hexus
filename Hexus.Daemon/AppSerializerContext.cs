using Hexus.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Contracts.Responses;
using System.Text.Json.Serialization;

namespace Hexus.Daemon;

[JsonSerializable(typeof(HttpValidationProblemDetails))]
[JsonSerializable(typeof(ApplicationResponse))]
[JsonSerializable(typeof(ApplicationLog))]
[JsonSerializable(typeof(IEnumerable<ApplicationResponse>))]
[JsonSerializable(typeof(IAsyncEnumerable<ApplicationLog>))]
[JsonSerializable(typeof(SendInputRequest))]
[JsonSerializable(typeof(GenericFailureResponse))]
[JsonSerializable(typeof(ReloadResult))]
[JsonSerializable(typeof(ConfigurationProblems))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
public partial class AppJsonSerializerContext : JsonSerializerContext;
