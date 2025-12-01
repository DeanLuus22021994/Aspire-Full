using System;
using System.Collections.Generic;
using Aspire_Full.Api.Diagnostics;
using Aspire_Full.Connectors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aspire_Full.Api.Controllers;

[ApiController]
[Route("api/connectors")]
public sealed class ConnectorDiagnosticsController : ControllerBase
{
    private readonly IConnectorMetricSnapshotProvider _snapshotProvider;
    private readonly IConnectorHealthRegistry _healthRegistry;
    private readonly IEvaluationOrchestrator _orchestrator;

    public ConnectorDiagnosticsController(
        IConnectorMetricSnapshotProvider snapshotProvider,
        IConnectorHealthRegistry healthRegistry,
        IEvaluationOrchestrator orchestrator)
    {
        _snapshotProvider = snapshotProvider;
        _healthRegistry = healthRegistry;
        _orchestrator = orchestrator;
    }

    [HttpGet("metrics")]
    [ProducesResponseType(typeof(ConnectorMetricsResponseDto), StatusCodes.Status200OK)]
    public ActionResult<ConnectorMetricsResponseDto> GetMetrics([FromQuery] int take = 100)
    {
        var safeTake = Math.Clamp(take, 10, 500);
        var snapshot = _snapshotProvider.BuildSnapshot(safeTake);
        return Ok(ConnectorMetricDtoMapper.FromSnapshot(snapshot));
    }

    [HttpGet("health")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ConnectorHealthSnapshot>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<ConnectorHealthSnapshot>> GetHealth()
        => Ok(_healthRegistry.GetAll());

    [HttpGet("evaluations")]
    [ProducesResponseType(typeof(IReadOnlyList<EvaluationRunRecord>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<EvaluationRunRecord>> GetEvaluations([FromQuery] int take = 50)
    {
        var safeTake = Math.Clamp(take, 1, 500);
        return Ok(_orchestrator.GetRecent(safeTake));
    }
}
