using System.Collections.Generic;

namespace ArcFaceSandbox.VectorStore;

/// <summary>
/// Represents a sandbox vector document with soft-delete metadata.
/// </summary>
public sealed record SandboxVectorDocument
{
    public required string Id { get; init; }
    public required string Content { get; init; }
    public required ReadOnlyMemory<float> Embedding { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}
