using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Aspire_Full.DevContainer;

/// <summary>
/// Canonical configuration for the Aspire devcontainer surface so AppHost and tests can rely on
/// shared constants instead of repeating inline strings.
/// </summary>
public static class DevContainerDefaults
{
    public const string ResourceName = "devcontainer";
    public const string DockerfileContext = "../.devcontainer";
    public const string SleepCommand = "sleep";
    public const string SleepForeverArgument = "infinity";
    public const string PythonVersion = "3.14.0-free-threaded+gil";
    public const string PythonRuntime = "cpython-free-threaded";
    public const string CudaRequirement = "cuda>=12.4,driver>=535";

    public static IReadOnlyList<(string Name, string Target)> VolumeMounts { get; } = new (string, string)[]
    {
        ("aspire-nuget-cache", "/home/vscode/.nuget"),
        ("aspire-dotnet-tools", "/home/vscode/.dotnet/tools"),
        ("aspire-aspire-cli", "/home/vscode/.aspire"),
        ("aspire-vscode-extensions", "/home/vscode/.vscode-server/extensions"),
        ("aspire-workspace", "/workspace"),
        ("aspire-docker-certs", "/certs"),
    };

    public static IReadOnlyDictionary<string, string> EnvironmentVariables { get; } =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_NOLOGO"] = "1",
                ["DOTNET_RUNNING_IN_CONTAINER"] = "true",
                ["NUGET_PACKAGES"] = "/home/vscode/.nuget/packages",
                ["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"] = "http://aspire-dashboard:18889",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://aspire-dashboard:18889",
                ["ASPIRE_DASHBOARD_MCP_ENDPOINT_URL"] = "http://aspire-dashboard:16036",
                ["ASPIRE_ALLOW_UNSECURED_TRANSPORT"] = "true",
                ["DOCKER_HOST"] = "tcp://docker:2376",
                ["DOCKER_TLS_VERIFY"] = "1",
                ["DOCKER_CERT_PATH"] = "/certs/client",
                ["NVIDIA_VISIBLE_DEVICES"] = "all",
                ["NVIDIA_DRIVER_CAPABILITIES"] = "compute,utility",
                ["NVIDIA_REQUIRE_CUDA"] = CudaRequirement,
                ["PYTHON_VERSION"] = PythonVersion,
                ["PYTHON_RUNTIME"] = PythonRuntime,
            });

    public static IReadOnlyList<string> BuildRuntimeArguments(string networkName, bool enableGpu = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(networkName);

        var args = new List<string> { "--network", networkName };
        if (enableGpu)
        {
            args.AddRange(new[] { "--gpus", "all" });
        }

        args.Add("--init");
        return args;
    }
}
