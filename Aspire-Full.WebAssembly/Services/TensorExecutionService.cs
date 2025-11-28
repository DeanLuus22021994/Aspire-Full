using Aspire_Full.Tensor;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Aspire_Full.WebAssembly.Services;

public interface ITensorExecutionService
{
    Task<TensorExecutionResult> ExecuteAsync(TensorExecutionRequest request, CancellationToken cancellationToken = default);
}

public sealed class TensorExecutionService : ITensorExecutionService, IAsyncDisposable
{
    private readonly ITensorRuntimeService _runtimeService;
    private readonly ILogger<TensorExecutionService> _logger;
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public TensorExecutionService(IJSRuntime jsRuntime, ITensorRuntimeService runtimeService, ILogger<TensorExecutionService> logger)
    {
        _runtimeService = runtimeService;
        _logger = logger;
        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/tensorInterop.js").AsTask());
    }

    public async Task<TensorExecutionResult> ExecuteAsync(TensorExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var capabilities = await _runtimeService.DetectCapabilitiesAsync(cancellationToken).ConfigureAwait(false);

        var executionPlan = request with
        {
            ExecutionProvider = string.IsNullOrWhiteSpace(request.ExecutionProvider)
                ? capabilities.RecommendedExecutionProvider
                : request.ExecutionProvider,
            SupportsSimd = capabilities.SupportsSimd,
            SupportsWebGpu = capabilities.SupportsWebGpu,
            SupportsWebGl2 = capabilities.SupportsWebGl2
        };

        var module = await _moduleTask.Value.ConfigureAwait(false);
        try
        {
            return await module.InvokeAsync<TensorExecutionResult>(
                "runTensorExecution",
                cancellationToken,
                executionPlan).ConfigureAwait(false);
        }
        catch (JSException ex)
        {
            _logger.LogError(ex, "Tensor execution via worker failed for model {ModelId}", request.ModelId);
            return TensorExecutionResult.Failed(request.ModelId, ex.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            try
            {
                var module = await _moduleTask.Value.ConfigureAwait(false);
                await module.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // JS runtime already disposed, nothing else to do
            }
        }
    }
}

public sealed record TensorExecutionRequest
{
    public required string ModelId { get; init; }
    public required string ModelUri { get; init; }
    public string ExecutionProvider { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string? InputImageUrl { get; init; }
    public bool SupportsWebGpu { get; init; }
    public bool SupportsWebGl2 { get; init; }
    public bool SupportsSimd { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record TensorExecutionChunk
{
    public required string Type { get; init; }
    public required string Content { get; init; }
    public int Sequence { get; init; }
    public double? Confidence { get; init; }
}

public sealed record TensorExecutionResult
{
    public required string ModelId { get; init; }
    public required string ExecutionProvider { get; init; }
    public required string Status { get; init; }
    public IReadOnlyList<TensorExecutionChunk> Chunks { get; init; } = Array.Empty<TensorExecutionChunk>();
    public double DurationMs { get; init; }
    public string? Error { get; init; }

    public static TensorExecutionResult Failed(string modelId, string? error) => new()
    {
        ModelId = modelId,
        ExecutionProvider = "wasm-cpu",
        Status = "Failed",
        Error = error,
        DurationMs = 0,
        Chunks = Array.Empty<TensorExecutionChunk>()
    };
}
