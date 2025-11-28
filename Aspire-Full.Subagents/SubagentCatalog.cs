using System.Collections.ObjectModel;
using System.Linq;

namespace Aspire_Full.Subagents;

public static class SubagentCatalog
{
    private static readonly IReadOnlyDictionary<SubagentRole, SubagentDefinition> _definitions =
        BuildDefinitions();

    public static IEnumerable<SubagentDefinition> All => _definitions.Values;

    public static SubagentDefinition Get(SubagentRole role)
    {
        if (_definitions.TryGetValue(role, out var definition))
        {
            return definition;
        }

        throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown subagent role");
    }

    private static IReadOnlyDictionary<SubagentRole, SubagentDefinition> BuildDefinitions()
    {
        var definitions = new Dictionary<SubagentRole, SubagentDefinition>
        {
            [SubagentRole.EmbeddingService] = new SubagentDefinition
            {
                Role = SubagentRole.EmbeddingService,
                Name = "Embedding Service",
                Directory = "subagents/embedding-service",
                Mission = "Manage ArcFace ONNX lifecycle and emit normalized 512-dim vectors.",
                UiPage = "Embedding Diagnostics",
            }.WithLists(
                inputs: new[]
                {
                    "Aligned 112x112 face crops",
                    "CUDA execution config",
                    "Model metadata",
                },
                outputs: new[]
                {
                    "Normalized 512 float vectors",
                    "Model telemetry",
                    "Hash validation events",
                },
                constraints: new[]
                {
                    "Reject invalid ONNX model hashes",
                    "GPU-only execution",
                    "Structured logging for inference results",
                },
                interfaces: new[] { "IArcFaceEmbeddingService", "AddArcFaceEmbedding" }),

            [SubagentRole.VectorStore] = new SubagentDefinition
            {
                Role = SubagentRole.VectorStore,
                Name = "Vector Store",
                Directory = "subagents/vector-store",
                Mission = "Own Qdrant orchestration pinned to 512-dimension schema.",
                UiPage = "Vector Store Monitor",
            }.WithLists(
                inputs: new[]
                {
                    "Embeddings and payload metadata",
                    "Vector store config",
                },
                outputs: new[]
                {
                    "Upsert/downsert confirmations",
                    "Search results",
                    "Collection health diagnostics",
                },
                constraints: new[]
                {
                    "Halt on non-512-dim embeddings",
                    "Auto-create collection with cosine distance",
                },
                interfaces: new[] { "ISandboxVectorStore", "AddSandboxVectorStore" }),

            [SubagentRole.UsersKernel] = new SubagentDefinition
            {
                Role = SubagentRole.UsersKernel,
                Name = "Users Kernel",
                Directory = "subagents/users-kernel",
                Mission = "Mirror production Users controller + EF Core flows inside sandbox.",
                UiPage = "Users Sandbox",
            }.WithLists(
                inputs: new[]
                {
                    "REST requests",
                    "Embeddings from Embedding Service",
                    "Sandbox DB connection",
                },
                outputs: new[]
                {
                    "JSON responses",
                    "Persistence changes",
                    "Vector sync events",
                },
                constraints: new[]
                {
                    "No shared state with production",
                    "Deterministic seed data",
                },
                interfaces: new[] { "IArcFaceEmbeddingService", "ISandboxVectorStore" }),
        };

        return new ReadOnlyDictionary<SubagentRole, SubagentDefinition>(definitions);
    }
}
