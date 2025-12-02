using System.Collections.Concurrent;

namespace Aspire_Full.Tensor.Core.Orchestration;

/// <summary>
/// Interface for tensor job persistence and retrieval.
/// </summary>
public interface ITensorJobStore
{
    Task<TensorJobStatus> UpsertAsync(TensorJobStatus job, CancellationToken cancellationToken = default);
    Task<TensorJobStatus?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TensorJobStatus>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory implementation of tensor job store for development and testing.
/// For production, implement a distributed store backed by Redis or database.
/// </summary>
public sealed class InMemoryTensorJobStore : ITensorJobStore
{
    private readonly ConcurrentDictionary<Guid, TensorJobStatus> _jobs = new();

    public Task<TensorJobStatus> UpsertAsync(TensorJobStatus job, CancellationToken cancellationToken = default)
    {
        _jobs.AddOrUpdate(job.Id, job, (_, _) => job);
        return Task.FromResult(job);
    }

    public Task<TensorJobStatus?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _jobs.TryGetValue(id, out var job);
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<TensorJobStatus>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        var items = _jobs.Values
            .OrderByDescending(job => job.CreatedAt)
            .Take(Math.Max(1, limit))
            .ToList();

        return Task.FromResult<IReadOnlyList<TensorJobStatus>>(items);
    }
}

/// <summary>
/// Tensor job status record for tracking compute operations.
/// </summary>
public sealed record TensorJobStatus
{
    public required Guid Id { get; init; }
    public required string ModelId { get; init; }
    public required string Status { get; init; }
    public string? Prompt { get; init; }
    public string? PromptPreview { get; init; }
    public string? InputImageUrl { get; init; }
    public string? ExecutionProvider { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? VectorDocumentId { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
