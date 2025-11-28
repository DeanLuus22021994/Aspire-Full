using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

namespace Aspire_Full.Connectors;

public interface IEvaluationOrchestrator
{
    Task RecordAsync(EvaluationRunRecord record, CancellationToken cancellationToken = default);
    IReadOnlyList<EvaluationRunRecord> GetRecent(int take = 20);
}

public sealed record EvaluationRunRecord(
    string RunId,
    string Scenario,
    string Metric,
    double Score,
    string? ModelId,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, string> Metadata);

internal sealed class InMemoryEvaluationOrchestrator : IEvaluationOrchestrator
{
    private const int MaxEntries = 200;
    private readonly ConcurrentQueue<EvaluationRunRecord> _records = new();

    public Task RecordAsync(EvaluationRunRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _records.Enqueue(record);
        while (_records.Count > MaxEntries && _records.TryDequeue(out _))
        {
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<EvaluationRunRecord> GetRecent(int take = 20)
        => _records.Reverse().Take(Math.Max(1, take)).ToImmutableList();
}
