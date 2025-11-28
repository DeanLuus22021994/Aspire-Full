using System;
using System.Linq;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Aspire_Full.DevContainer;

/// <summary>
/// Extension helpers that expose the devcontainer Dockerfile as a first-class Aspire resource.
/// </summary>
public static class DevContainerResourceBuilderExtensions
{
    public static IResourceBuilder<DockerfileResource> AddDevContainer(
        this IDistributedApplicationBuilder builder,
        string networkName,
        bool enableGpu = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(networkName);

        var resource = builder.AddDockerfile(DevContainerDefaults.ResourceName, DevContainerDefaults.DockerfileContext)
            .WithArgs(DevContainerDefaults.SleepCommand, DevContainerDefaults.SleepForeverArgument)
            .WithLifetime(ContainerLifetime.Persistent);

        foreach (var volume in DevContainerDefaults.VolumeMounts)
        {
            resource = resource.WithVolume(volume.Name, volume.Target);
        }

        foreach (var envVar in DevContainerDefaults.EnvironmentVariables)
        {
            resource = resource.WithEnvironment(envVar.Key, envVar.Value);
        }

        var runtimeArgs = DevContainerDefaults.BuildRuntimeArguments(networkName, enableGpu);
        return resource.WithContainerRuntimeArgs(runtimeArgs.ToArray());
    }
}
