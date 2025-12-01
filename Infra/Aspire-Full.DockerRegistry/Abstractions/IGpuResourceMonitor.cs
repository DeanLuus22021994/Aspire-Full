using System;
using Aspire_Full.Tensor.Core.Abstractions;

namespace Aspire_Full.DockerRegistry.Abstractions;

/// <summary>
/// DockerRegistry-specific interface for GPU resource monitoring.
/// Currently equivalent to IGpuResourceMonitor, but allows for future
/// DockerRegistry-specific extensions without breaking other consumers.
/// </summary>
public interface IDockerRegistryGpuMonitor : IGpuResourceMonitor
{
    // Future DockerRegistry-specific members can be added here
}
