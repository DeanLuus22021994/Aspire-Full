using System;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Abstractions;
using Aspire_Full.DockerRegistry.Configuration;
using Aspire_Full.DockerRegistry.Models;
using Aspire_Full.DockerRegistry.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.DockerRegistry.Services;

public class ImageValidationPipeline
{
    private readonly ILogger<ImageValidationPipeline> _logger;
    private readonly RegistryConfiguration _config;

    public ImageValidationPipeline(
        ILogger<ImageValidationPipeline> logger,
        IOptions<RegistryConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public async Task<bool> ValidateManifestAsync(DockerManifest manifest, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating manifest for {Repository}:{Tag}", manifest.Repository, manifest.Tag);

        if (_config.Validation.ValidateTensorContent)
        {
            // In a real scenario, we would download the layer and validate it.
            // Here we simulate validation using the native context if available.
            try
            {
                if (NativeTensorContext.InitTensorContext() > 0)
                {
                    var metrics = new NativeTensorContext.TensorMetrics();
                    // Simulate data
                    float[] dummyData = new float[1024];
                    int result = NativeTensorContext.ValidateTensorContent(dummyData, dummyData.Length, 0.5f, ref metrics);

                    if (result != 1)
                    {
                        _logger.LogError("Native tensor validation failed.");
                        return false;
                    }
                    _logger.LogInformation("Native tensor validation passed. Compute time: {Time}ms", metrics.compute_time_ms);
                }
                else
                {
                    _logger.LogWarning("Native tensor context not available. Skipping native validation.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during native validation.");
                if (_config.Validation.TensorCheckStrictness == "Strict")
                {
                    return false;
                }
            }
        }

        return true;
    }
}
