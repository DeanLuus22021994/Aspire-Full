using System.Collections.Generic;
using System.Linq;
using Aspire_Full.Connectors;

namespace Aspire_Full.Tests.Unit.Connectors;

public sealed class ConnectorMetricSnapshotProviderTests
{
    [Fact]
    public void BuildSnapshot_GroupsByDimensionAndCalculatesAverages()
    {
        var orchestrator = new FakeEvaluationOrchestrator();
        orchestrator.Seed(new EvaluationRunRecord(
            RunId: "run-1",
            Scenario: "Evaluation",
            Metric: "Code Quality",
            Score: 0.9,
            ModelId: null,
            Timestamp: DateTimeOffset.UtcNow,
            Metadata: new Dictionary<string, string>
            {
                [ConnectorTraceTags.Category] = nameof(ConnectorMetricDimension.CodeQuality),
                [ConnectorTraceTags.Detail] = "clean",
                [ConnectorTraceTags.MetricName] = "Code Quality"
            }));

        orchestrator.Seed(new EvaluationRunRecord(
            RunId: "run-2",
            Scenario: "Evaluation",
            Metric: "Code Quality",
            Score: 0.6,
            ModelId: null,
            Timestamp: DateTimeOffset.UtcNow.AddMinutes(-2),
            Metadata: new Dictionary<string, string>
            {
                [ConnectorTraceTags.Category] = nameof(ConnectorMetricDimension.CodeQuality),
                [ConnectorTraceTags.Detail] = "warnings",
                [ConnectorTraceTags.MetricName] = "Code Quality"
            }));

        var provider = new ConnectorMetricSnapshotProvider(orchestrator);

        var snapshot = provider.BuildSnapshot();

        snapshot.GeneratedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
        snapshot.Summaries.Should().ContainSingle();
        var summary = snapshot.Summaries[0];
        summary.Dimension.Should().Be(ConnectorMetricDimension.CodeQuality);
        summary.Observations.Should().Be(2);
        summary.Status.Should().Be(ConnectorMetricStatus.Pass);
        summary.AverageScore.Should().BeApproximately(0.75, 0.001);
        summary.Samples.Should().HaveCount(2);
        summary.Samples[0].Detail.Should().Be("clean");
    }

    private sealed class FakeEvaluationOrchestrator : IEvaluationOrchestrator
    {
        private readonly List<EvaluationRunRecord> _records = new();

        public void Seed(EvaluationRunRecord record) => _records.Add(record);

        public IReadOnlyList<EvaluationRunRecord> GetRecent(int take = 20)
            => _records.Take(take).ToArray();

        public Task RecordAsync(EvaluationRunRecord record, CancellationToken cancellationToken = default)
        {
            _records.Add(record);
            return Task.CompletedTask;
        }
    }
}
