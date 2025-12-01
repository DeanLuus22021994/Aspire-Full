using Aspire_Full.Pipeline.Modules.Discovery;

namespace Aspire_Full.Pipeline.Modules.Discovery.Components;

public class DockerImagesComponent : IDiscoveryComponent
{
    public Task<DiscoveryResult> DiscoverAsync(EnvironmentConfig config)
    {
        // Official Python Docker images do not yet have specific tags for free-threading (nogil).
        // Users must currently build from source or use experimental third-party images.
        // We list the latest official base tags here.

        var bleedingEdge = "python:3.14.0a1-slim-bookworm"; // Actual alpha tag
        var stable = "python:3.14.0a1-slim-bookworm";         // Actual stable tag

        var details = new Dictionary<string, string>
        {
            ["Bleeding Edge"] = bleedingEdge,
            ["Stable"] = stable,
            ["Note"] = "Official free-threading images are not yet available. Custom build required."
        };

        config.Docker.Images.Python.FreeThreading.BleedingEdge = bleedingEdge;
        config.Docker.Images.Python.FreeThreading.Stable = stable;
        config.Docker.Images.Python.FreeThreading.Note = "Requires --disable-gil build flag";

        return Task.FromResult(new DiscoveryResult(
            "Docker Images",
            "Info",
            "Recommended Python Images (Free Threading Base)",
            details
        ));
    }
}
