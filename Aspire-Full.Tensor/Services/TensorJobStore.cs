using System.Collections.Concurrent;
using System.Linq;
using Aspire_Full.Tensor.Models;

namespace Aspire_Full.Tensor.Services;

public interface ITensorJobStore
{
    Task<TensorJobStatusDto> UpsertAsync(TensorJobStatusDto job, CancellationToken cancellationToken = default);
    Task<TensorJobStatusDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TensorJobStatusDto>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}

public sealed class InMemoryTensorJobStore : ITensorJobStore
{
    private readonly ConcurrentDictionary<Guid, TensorJobStatusDto> _jobs = new();

    public Task<TensorJobStatusDto> UpsertAsync(TensorJobStatusDto job, CancellationToken cancellationToken = default)
    {
        _jobs.AddOrUpdate(job.Id, job, (_, _) => job);
        return Task.FromResult(job);
    }

    public Task<TensorJobStatusDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _jobs.TryGetValue(id, out var job);
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<TensorJobStatusDto>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        var items = _jobs.Values
            .OrderByDescending(job => job.CreatedAt)
            .Take(Math.Max(1, limit))
            .ToList();

        return Task.FromResult<IReadOnlyList<TensorJobStatusDto>>(items);
    }
}
