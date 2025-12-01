using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Aspire_Full.Shared.Models;

// Force re-index
public sealed record TensorJobSubmission
{
    [Required]
    public required string ModelId { get; init; }

    [Required]
    [MaxLength(2048)]
    public string Prompt { get; init; } = string.Empty;

    public string? InputImageUrl { get; init; }
    public string ExecutionProvider { get; init; } = string.Empty;
    public bool PersistToVectorStore { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record TensorJobSummary
{
    public required Guid Id { get; init; }
    public required string ModelId { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? ExecutionProvider { get; init; }
    public string PromptPreview { get; init; } = string.Empty;
}

public sealed record TensorJobStatus
{
    public required Guid Id { get; init; }
    public required string ModelId { get; init; }
    public required string Status { get; init; }
    public string Prompt { get; init; } = string.Empty;
    public string PromptPreview { get; init; } = string.Empty;
    public string? InputImageUrl { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? ExecutionProvider { get; init; }
    public string? VectorDocumentId { get; init; }
    public IList<TensorInferenceChunk> Output { get; init; } = new List<TensorInferenceChunk>();
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record TensorInferenceChunk
{
    public required string Type { get; init; }
    public required string Content { get; init; }
    public int Sequence { get; init; }
    public double? Confidence { get; init; }
}

public sealed record TensorModelSummary
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? DocumentationUri { get; init; }
    public string? RecommendedExecutionProvider { get; init; }
}
