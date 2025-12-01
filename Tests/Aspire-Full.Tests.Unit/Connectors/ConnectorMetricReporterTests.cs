using System.Collections.Generic;
using System.Linq;
using Aspire_Full.Connectors;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire_Full.Tests.Unit.Connectors;

public sealed class ConnectorMetricReporterTests
{
    [Fact]
    public async Task ReportAsync_PopulatesMetadataAndPersistsRecord()
    {
        var orchestrator = new FakeEvaluationOrchestrator();
        var reporter = new ConnectorMetricReporter(orchestrator, NullLogger<ConnectorMetricReporter>.Instance);
        var metadata = new Dictionary<string, string> { ["model_id"] = "tensor-eu" };
        var report = new ConnectorMetricReport(
            ConnectorMetricDimension.VectorStoreQuality,
            Score: 0.95,
            Status: ConnectorMetricStatus.Pass,
            Detail: "variance OK",
            Metadata: metadata);

        await reporter.ReportAsync(report);

        orchestrator.Records.Should().ContainSingle();
        var record = orchestrator.Records[0];
        record.Scenario.Should().Be("Evaluation");
        record.Metadata[ConnectorTraceTags.Category].Should().Be(nameof(ConnectorMetricDimension.VectorStoreQuality));
        record.Metadata[ConnectorTraceTags.Status].Should().Be(nameof(ConnectorMetricStatus.Pass));
        record.Metadata[ConnectorTraceTags.Score].Should().Be("0.95");
        record.Metadata[ConnectorTraceTags.Detail].Should().Be("variance OK");
        record.Metadata[ConnectorTraceTags.MetricName].Should().Be("Vector Store Quality");
    }

    private sealed class FakeEvaluationOrchestrator : IEvaluationOrchestrator
    {
        private readonly List<EvaluationRunRecord> _records = new();

        public IReadOnlyList<EvaluationRunRecord> Records => _records;

        public IReadOnlyList<EvaluationRunRecord> GetRecent(int take = 20)
            => _records.Take(take).ToArray();

        public Task RecordAsync(EvaluationRunRecord record, CancellationToken cancellationToken = default)
        {
            _records.Add(record);
            return Task.CompletedTask;
        }
    }
}
