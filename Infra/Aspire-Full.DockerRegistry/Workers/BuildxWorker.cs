using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Abstractions;
using Aspire_Full.DockerRegistry.Configuration;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.DockerRegistry.Workers;

internal sealed class BuildxWorker : IBuildxWorker
{
    private readonly ILogger _logger;
    private readonly GpuAccelerationOptions _gpuOptions;

    public string Id { get; }

    public BuildxWorker(string id, ILogger logger, GpuAccelerationOptions gpuOptions)
    {
        Id = id;
        _logger = logger;
        _gpuOptions = gpuOptions;
    }

    public async Task ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Worker {WorkerId} executing: {Command} (GPU: {GpuEnabled})", Id, command, _gpuOptions.Enabled);

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"buildx {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Configure GPU environment variables when GPU acceleration is enabled
        if (_gpuOptions.Enabled)
        {
            startInfo.EnvironmentVariables["NVIDIA_VISIBLE_DEVICES"] = "all";
            startInfo.EnvironmentVariables["NVIDIA_DRIVER_CAPABILITIES"] = "compute,utility";
            startInfo.EnvironmentVariables["NVIDIA_REQUIRE_CUDA"] = $"cuda>={_gpuOptions.MinimumCudaVersion},driver>={_gpuOptions.MinimumDriverVersion}";
            startInfo.EnvironmentVariables["TORCH_CUDA_ARCH_LIST"] = _gpuOptions.TorchCudaArchList;
            startInfo.EnvironmentVariables["CUDA_CACHE_PATH"] = "/var/cache/cuda";
            startInfo.EnvironmentVariables["CCACHE_DIR"] = "/root/.ccache";
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError("Worker {WorkerId} failed with exit code {ExitCode}. Error: {Error}", Id, process.ExitCode, error);
            throw new DockerRegistryException(DockerRegistryErrorCode.BuildxError, $"Buildx command failed: {error}");
        }

        _logger.LogInformation("Worker {WorkerId} completed successfully. Output: {Output}", Id, output);
    }

    /// <summary>
    /// Executes a buildx build command with GPU passthrough for TensorCore compilation.
    /// </summary>
    public async Task ExecuteBuildWithGpuAsync(string dockerfile, string context, string tag, CancellationToken cancellationToken = default)
    {
        var gpuFlag = _gpuOptions.Enabled ? "--allow security.insecure" : "";
        var cacheArgs = _gpuOptions.Enabled
            ? $"--cache-from=type=local,src=/var/cache/buildkit --cache-to=type=local,dest=/var/cache/buildkit,mode=max"
            : "";

        var command = $"build {gpuFlag} {cacheArgs} -f {dockerfile} -t {tag} {context}";
        await ExecuteCommandAsync(command, cancellationToken);
    }
}
