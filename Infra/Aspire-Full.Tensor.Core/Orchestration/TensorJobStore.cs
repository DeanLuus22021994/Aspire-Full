using System.Collections.Concurrent;
using Aspire_Full.Shared.Models;

namespace Aspire_Full.Tensor.Core.Orchestration;

/// <summary>
/// Interface for tensor job persistence and retrieval.
/// Uses <see cref="TensorJobStatus"/> from Aspire_Full.Shared.Models.
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
