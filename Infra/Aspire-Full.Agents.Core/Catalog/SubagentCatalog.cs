using System.Collections.ObjectModel;
using Aspire_Full.Shared.Models;

namespace Aspire_Full.Agents.Core.Catalog;

/// <summary>
/// Provides static subagent definitions for all roles.
/// Implements <see cref="ISubagentCatalog"/> for DI compatibility.
/// </summary>
public sealed class SubagentCatalog : ISubagentCatalog
{
    private readonly IReadOnlyDictionary<SubagentRole, SubagentDefinition> _definitions;

    public SubagentCatalog()
    {
        _definitions = BuildDefinitions();
    }

    public IEnumerable<SubagentDefinition> All => _definitions.Values;

    public SubagentDefinition Get(SubagentRole role)
    {
        if (_definitions.TryGetValue(role, out var definition))
        {
            return definition;
        }

        throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown subagent role");
    }

    public bool TryGet(SubagentRole role, out SubagentDefinition? definition)
    {
        var found = _definitions.TryGetValue(role, out var def);
        definition = def;
        return found;
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
                inputs:
                [
                    "Aligned 112x112 face crops",
                    "CUDA execution config",
                    "Model metadata"
                ],
                outputs:
                [
                    "Normalized 512 float vectors",
                    "Model telemetry",
                    "Hash validation events"
                ],
                constraints:
                [
                    "Reject invalid ONNX model hashes",
                    "GPU-only execution",
                    "Structured logging for inference results"
                ],
                interfaces: ["IArcFaceEmbeddingService", "AddArcFaceEmbedding"]),

            [SubagentRole.VectorStore] = new SubagentDefinition
            {
                Role = SubagentRole.VectorStore,
                Name = "Vector Store",
                Directory = "subagents/vector-store",
                Mission = "Own Qdrant orchestration pinned to 512-dimension schema.",
                UiPage = "Vector Store Monitor",
            }.WithLists(
                inputs:
                [
                    "Embeddings and payload metadata",
                    "Vector store config"
                ],
                outputs:
                [
                    "Upsert/downsert confirmations",
                    "Search results",
                    "Collection health diagnostics"
                ],
                constraints:
                [
                    "Halt on non-512-dim embeddings",
                    "Auto-create collection with cosine distance"
                ],
                interfaces: ["ISandboxVectorStore", "AddSandboxVectorStore"]),

            [SubagentRole.UsersKernel] = new SubagentDefinition
            {
                Role = SubagentRole.UsersKernel,
                Name = "Users Kernel",
                Directory = "subagents/users-kernel",
                Mission = "Mirror production Users controller + EF Core flows inside sandbox.",
                UiPage = "Users Sandbox",
            }.WithLists(
                inputs:
                [
                    "REST requests",
                    "Embeddings from Embedding Service",
                    "Sandbox DB connection"
                ],
                outputs:
                [
                    "JSON responses",
                    "Persistence changes",
                    "Vector sync events"
                ],
                constraints:
                [
                    "No shared state with production",
                    "Deterministic seed data"
                ],
                interfaces: ["IArcFaceEmbeddingService", "ISandboxVectorStore"]),
        };

        return new ReadOnlyDictionary<SubagentRole, SubagentDefinition>(definitions);
    }
}
