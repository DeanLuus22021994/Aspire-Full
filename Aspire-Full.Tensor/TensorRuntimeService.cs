using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Aspire_Full.Tensor;

public interface ITensorRuntimeService
{
    Task<TensorCapabilityResponse> DetectCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<TensorModelDescriptor?> ResolveModelAsync(string modelId, CancellationToken cancellationToken = default);
}

public sealed class TensorRuntimeService(ILogger<TensorRuntimeService> logger, IJSRuntime jsRuntime, IOptions<TensorModelCatalogOptions> options) : ITensorRuntimeService
{
    private readonly ILogger<TensorRuntimeService> _logger = logger;
    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private readonly IReadOnlyDictionary<string, TensorModelDescriptor> _catalog =
        (options.Value.Models ?? new List<TensorModelDescriptor>())
            .Where(static m => !string.IsNullOrWhiteSpace(m.Id))
            .GroupBy(static m => m.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

    public async Task<TensorCapabilityResponse> DetectCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _jsRuntime.InvokeAsync<TensorCapabilityResponse>("AspireTensor.determineRuntime", cancellationToken);
            return response;
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Unable to detect tensor runtime capabilities via JS");
            return new TensorCapabilityResponse
            {
                SupportsWebGl2 = false,
                SupportsWebGpu = false,
                SupportsSimd = false,
                RecommendedExecutionProvider = "wasm-cpu",
                Metadata = new Dictionary<string, string> { ["error"] = ex.Message }
            };
        }
    }

    public Task<TensorModelDescriptor?> ResolveModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        _catalog.TryGetValue(modelId, out var descriptor);
        return Task.FromResult(descriptor);
    }
}
