using Aspire_Full.Pipeline.Modules.Discovery;

namespace Aspire_Full.Pipeline.Modules.Discovery.Components;

public class DockerImagesComponent : IDiscoveryComponent
{
    // Python 3.15.0a2 free-threaded (3.15t) - GIL disabled by default
    // Installed via: uv python install 3.15t
    private const string PythonVersion = "3.15t";
    private const string PythonVersionFull = "3.15.0a2";

    public Task<DiscoveryResult> DiscoverAsync(EnvironmentConfig config)
    {
        // Python 3.15t free-threaded is installed via uv, not from Docker Hub images.
        // The base images use CUDA bootstrap + uv to install Python dynamically.
        // This provides platform-agnostic 64-bit free-threaded Python builds.

        var bleedingEdge = $"uv python install {PythonVersion}"; // Dynamic via uv
        var stable = "python:3.14t-slim-bookworm";               // Fallback stable

        var details = new Dictionary<string, string>
        {
            ["Version"] = PythonVersionFull,
            ["Free-Threading"] = PythonVersion,
            ["Install Method"] = bleedingEdge,
            ["Stable Fallback"] = stable,
            ["GIL Status"] = "Disabled (PYTHON_GIL=0)",
            ["JIT Status"] = "Enabled (PYTHON_JIT=1)",
            ["Note"] = "Installed via Astral uv for platform-agnostic free-threaded builds"
        };

        config.Docker.Images.Python.FreeThreading.BleedingEdge = bleedingEdge;
        config.Docker.Images.Python.FreeThreading.Stable = stable;
        config.Docker.Images.Python.FreeThreading.Note = "GIL disabled by default in 3.15t builds";

        return Task.FromResult(new DiscoveryResult(
            "Docker Images",
            "Info",
            $"Python {PythonVersionFull} Free-Threaded ({PythonVersion})",
            details
        ));
    }
}
