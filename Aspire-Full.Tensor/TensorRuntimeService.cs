using System.Diagnostics;
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
        using var activity = TensorDiagnostics.ActivitySource.StartActivity("TensorRuntime.DetectCapabilities");
        try
        {
            var response = await _jsRuntime.InvokeAsync<TensorCapabilityResponse>("AspireTensor.determineRuntime", cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("tensor.runtime.webgpu", response.SupportsWebGpu);
            activity?.SetTag("tensor.runtime.webgl2", response.SupportsWebGl2);
            activity?.SetTag("tensor.runtime.simd", response.SupportsSimd);
            activity?.SetTag("tensor.runtime.recommended_provider", response.RecommendedExecutionProvider);
            return response;
        }
        catch (JSException ex)
        {
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                ["exception.type"] = ex.GetType().FullName ?? "JSException",
                ["exception.message"] = ex.Message
            }));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
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
        using var activity = TensorDiagnostics.ActivitySource.StartActivity("TensorRuntime.ResolveModel");
        activity?.SetTag("tensor.model_id", modelId);
        _catalog.TryGetValue(modelId, out var descriptor);
        if (descriptor is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "model_not_found");
        }
        else
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        return Task.FromResult(descriptor);
    }
}
