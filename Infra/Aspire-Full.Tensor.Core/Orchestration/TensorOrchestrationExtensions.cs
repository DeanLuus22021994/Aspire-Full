using Aspire_Full.Tensor.Core.Orchestration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering tensor orchestration services.
/// </summary>
public static class TensorOrchestrationExtensions
{
    /// <summary>
    /// Adds tensor orchestration services for job coordination and tracking.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Optional configuration for orchestration options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTensorOrchestration(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        // Register job store - defaults to in-memory, can be overridden
        services.AddSingleton<ITensorJobStore, InMemoryTensorJobStore>();

        // Configure orchestration options if provided
        if (configuration != null)
        {
            services.Configure<TensorOrchestrationOptions>(
                configuration.GetSection("Tensor:Orchestration"));
        }

        return services;
    }
}

/// <summary>
/// Configuration options for tensor orchestration.
/// </summary>
public sealed class TensorOrchestrationOptions
{
    /// <summary>
    /// Maximum concurrent jobs allowed. Default is 16.
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 16;

    /// <summary>
    /// Job timeout in seconds. Default is 300 (5 minutes).
    /// </summary>
    public int JobTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Whether to persist completed jobs to vector store. Default is false.
    /// </summary>
    public bool PersistToVectorStore { get; set; } = false;

    /// <summary>
    /// Maximum jobs to retain in memory. Default is 1000.
    /// </summary>
    public int MaxRetainedJobs { get; set; } = 1000;
}
