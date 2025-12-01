using System.Text.Json;
using System.Text.Json.Serialization;
using Aspire_Full.Shared.Models;

namespace Aspire_Full.Shared;

[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(AgentInput))]
[JsonSerializable(typeof(AgentOutput))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(TensorJobSubmission))]
[JsonSerializable(typeof(TensorJobSummary))]
[JsonSerializable(typeof(List<TensorJobSummary>))]
[JsonSerializable(typeof(TensorJobStatus))]
[JsonSerializable(typeof(TensorInferenceChunk))]
[JsonSerializable(typeof(TensorModelSummary))]
[JsonSerializable(typeof(List<TensorModelSummary>))]
[JsonSerializable(typeof(CreateUser))]
[JsonSerializable(typeof(UpdateUser))]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(List<User>))]
[JsonSerializable(typeof(DockerRegistryRepository))]
[JsonSerializable(typeof(List<DockerRegistryRepository>))]
[JsonSerializable(typeof(DockerManifest))]
[JsonSerializable(typeof(DockerManifestLayer))]
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    UseStringEnumConverter = true,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
public partial class AppJsonContext : JsonSerializerContext
{
}
