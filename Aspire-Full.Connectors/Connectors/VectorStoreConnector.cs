using System.Diagnostics;
using System.Linq;
using Aspire_Full.VectorStore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.Connectors;

public interface IVectorStoreConnector
{
    Task<VectorStoreConnectorResult> UpsertAsync(VectorStoreConnectorRequest request, CancellationToken cancellationToken = default);
}

public sealed record VectorStoreConnectorRequest(
    string? DocumentId,
    string Content,
    ReadOnlyMemory<float> Embedding,
    IReadOnlyDictionary<string, object?> Metadata);

public sealed record VectorStoreConnectorResult(bool Success, string? DocumentId, string? Error);

internal sealed class VectorStoreConnector : IVectorStoreConnector
{
    private readonly IVectorStoreService _vectorStoreService;
    private readonly ILogger<VectorStoreConnector> _logger;
    private readonly IConnectorHealthRegistry _healthRegistry;
    private readonly string _collectionName;

    public VectorStoreConnector(
        IVectorStoreService vectorStoreService,
        IOptions<ConnectorHubOptions> options,
        IConnectorHealthRegistry healthRegistry,
        ILogger<VectorStoreConnector> logger)
    {
        _vectorStoreService = vectorStoreService;
        _logger = logger;
        _healthRegistry = healthRegistry;
        _collectionName = options.Value.VectorStore.CollectionName;
    }

    public async Task<VectorStoreConnectorResult> UpsertAsync(VectorStoreConnectorRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = ConnectorDiagnostics.ActivitySource.StartActivity("VectorStoreConnector.Upsert");
        activity?.SetTag("connector.collection", _collectionName);
        activity?.SetTag("connector.embedding_length", request.Embedding.Length);

        try
        {
            var documentId = string.IsNullOrWhiteSpace(request.DocumentId) ? Guid.NewGuid().ToString() : request.DocumentId;
            var document = new VectorDocument
            {
                Id = documentId,
                Content = request.Content,
                Embedding = request.Embedding,
                Metadata = request.Metadata?.ToDictionary(static pair => pair.Key, static pair => pair.Value ?? string.Empty)
            };

            await _vectorStoreService.EnsureCollectionAsync(_collectionName, document.Embedding.Length, cancellationToken).ConfigureAwait(false);
            await _vectorStoreService.UpsertAsync(document, cancellationToken).ConfigureAwait(false);

            _healthRegistry.ReportHealthy("vector-store", $"Last upsert at {DateTimeOffset.UtcNow:O}");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return new VectorStoreConnectorResult(true, documentId, null);
        }
        catch (Exception ex)
        {
            _healthRegistry.ReportUnhealthy("vector-store", ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                ["exception.type"] = ex.GetType().FullName ?? "Exception",
                ["exception.message"] = ex.Message
            }));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Vector store upsert failed");
            return new VectorStoreConnectorResult(false, null, ex.Message);
        }
    }
}
