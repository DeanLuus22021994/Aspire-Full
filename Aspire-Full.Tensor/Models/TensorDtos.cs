using System.ComponentModel.DataAnnotations;

namespace Aspire_Full.Tensor.Models;

public sealed record TensorJobSubmissionDto
{
    [Required]
    public string ModelId { get; init; } = string.Empty;

    [Required]
    [MaxLength(2048)]
    public string Prompt { get; init; } = string.Empty;

    public string? InputImageUrl { get; init; }

    public string ExecutionProvider { get; init; } = string.Empty;

    public bool PersistToVectorStore { get; init; }

    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record TensorJobSummaryDto
{
    public required Guid Id { get; init; }
    public required string ModelId { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? ExecutionProvider { get; init; }
    public string PromptPreview { get; init; } = string.Empty;
}

public sealed record TensorInferenceChunkDto
{
    public required string Type { get; init; }
    public required string Content { get; init; }
    public int Sequence { get; init; }
    public double? Confidence { get; init; }
}

public sealed record TensorJobStatusDto
{
    public required Guid Id { get; init; }
    public required string ModelId { get; init; }
    public required string Status { get; init; }
    public required string Prompt { get; init; }
    public string PromptPreview { get; init; } = string.Empty;
    public string? InputImageUrl { get; init; }
    public string? ExecutionProvider { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? VectorDocumentId { get; init; }
    public IList<TensorInferenceChunkDto> Output { get; init; } = new List<TensorInferenceChunkDto>();
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record TensorModelSummaryDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? DocumentationUri { get; init; }
    public string? RecommendedExecutionProvider { get; init; }
}
