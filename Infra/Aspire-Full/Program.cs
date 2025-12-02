using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Qdrant;
using Aspire_Full.Configuration;
using Aspire_Full.DevContainer;
using Microsoft.Extensions.Configuration;

// =============================================================================
// Aspire Full AppHost - Distributed Application Orchestrator
// =============================================================================

var builder = DistributedApplication.CreateBuilder(args);

// Load Configuration - prefer unified config, fall back to legacy
var settings = ConfigLoader.LoadSettings(".aspire/settings.json");
var aspireConfig = ConfigLoader.LoadAspireConfig();

// External network for container-to-container communication
const string networkName = AppHostConstants.NetworkName;
const string dockerDebuggerImage = AppHostConstants.Images.DockerDebugger;

// -----------------------------------------------------------------------------
// Dev Infrastructure - Docker-in-Docker daemon + dashboard + devcontainer
// -----------------------------------------------------------------------------
var dockerDaemon = builder.AddContainer(AppHostConstants.Resources.DockerDaemon, AppHostConstants.Images.DockerDind)
    .WithVolume(AppHostConstants.Volumes.DockerData, AppHostConstants.Paths.DockerLib)
    .WithVolume(AppHostConstants.Volumes.DockerCerts, AppHostConstants.Paths.Certs)
    .WithEnvironment(AppHostConstants.EnvironmentVariables.DockerTlsCertDir, AppHostConstants.Paths.Certs)
    .WithContainerRuntimeArgs("--host=tcp://0.0.0.0:2376", "--host=unix:///var/run/docker.sock")
    .WithContainerRuntimeArgs("--network", networkName)
    .WithHttpEndpoint(name: "engine", port: AppHostConstants.Ports.DockerEngine, targetPort: 2376)
    .WithLifetime(ContainerLifetime.Persistent);

var dockerDebugger = builder.AddContainer(AppHostConstants.Resources.DockerDebugger, dockerDebuggerImage)
    .WaitFor(dockerDaemon)
    .WithVolume(AppHostConstants.Volumes.DockerCerts, AppHostConstants.Paths.Certs)
    .WithEnvironment(AppHostConstants.EnvironmentVariables.DockerHost, $"tcp://{AppHostConstants.Resources.DockerDaemon}:2376")
    .WithEnvironment(AppHostConstants.EnvironmentVariables.DockerTlsVerify, "1")
    .WithEnvironment(AppHostConstants.EnvironmentVariables.DockerCertPath, AppHostConstants.Paths.CertsClient)
    .WithEnvironment(AppHostConstants.EnvironmentVariables.DockerDebuggerTargetNetwork, networkName)
    .WithHttpEndpoint(name: "debugger-ui", port: AppHostConstants.Ports.DockerDebuggerUi, targetPort: 9393)
    .WithContainerRuntimeArgs("--network", networkName)
    .WithLifetime(ContainerLifetime.Persistent);

// Dashboard is provided automatically by Aspire in local dev mode
// This containerized dashboard is only needed for Docker/Kubernetes deployments
// Uncomment for production container deployments:
// var dashboard = builder.AddContainer(AppHostConstants.Resources.AspireDashboard, AppHostConstants.Images.AspireDashboard)
//     .WithVolume(AppHostConstants.Volumes.DashboardData, AppHostConstants.Paths.DashboardData)
//     .WithEnvironment(AppHostConstants.EnvironmentVariables.DotnetDashboardUnsecuredAllowAnonymous, "true")
//     .WithEnvironment(AppHostConstants.EnvironmentVariables.DashboardOtlpAuthMode, "Unsecured")
//     .WithEnvironment(AppHostConstants.EnvironmentVariables.DashboardFrontendAuthMode, "Unsecured")
//     .WithEnvironment(AppHostConstants.EnvironmentVariables.DashboardResourceServiceAuthMode, "Unsecured")
//     .WithEnvironment(AppHostConstants.EnvironmentVariables.AspireDashboardMcpEndpointUrl, "http://0.0.0.0:16036")
//     .WithEnvironment(AppHostConstants.EnvironmentVariables.AspireAllowUnsecuredTransport, "true")
//     .WithHttpEndpoint(name: "http", port: AppHostConstants.Ports.DashboardUi, targetPort: 18888)
//     .WithHttpEndpoint(name: "otlp", port: AppHostConstants.Ports.DashboardOtlp, targetPort: 18889)
//     .WithContainerRuntimeArgs("--network", networkName)
//     .WithLifetime(ContainerLifetime.Persistent);

// -----------------------------------------------------------------------------
// Internal Docker Registry - Local artifact cache
// -----------------------------------------------------------------------------
var registry = builder.AddContainer(AppHostConstants.Resources.Registry, AppHostConstants.Images.Registry)
    .WithVolume(settings.Registry.VolumeName, AppHostConstants.Paths.RegistryData)
    .WithHttpEndpoint(name: "registry", port: settings.Registry.Port, targetPort: 5000)
    .WithContainerRuntimeArgs("--network", networkName)
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddDevContainer(networkName);

// -----------------------------------------------------------------------------
// Database Layer - PostgreSQL with pgvector for semantic search
// -----------------------------------------------------------------------------
var postgres = builder.AddPostgres(AppHostConstants.Resources.Postgres)
    .WithPgAdmin()
    .WithDataVolume(AppHostConstants.Volumes.PostgresData)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerRuntimeArgs("--network", networkName);

var database = postgres.AddDatabase(AppHostConstants.Resources.Database);

// -----------------------------------------------------------------------------
// Cache Layer - Redis for session and distributed caching
// -----------------------------------------------------------------------------
var redis = builder.AddRedis(AppHostConstants.Resources.Redis)
    .WithRedisCommander()
    .WithDataVolume(AppHostConstants.Volumes.RedisData)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerRuntimeArgs("--network", networkName);

// -----------------------------------------------------------------------------
// Vector Database - Qdrant for semantic search and embeddings
// -----------------------------------------------------------------------------
var qdrant = builder.AddQdrant(AppHostConstants.Resources.Qdrant)
    .WithDataVolume(AppHostConstants.Volumes.QdrantData)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerRuntimeArgs("--network", networkName);

// -----------------------------------------------------------------------------
// Application Services
// -----------------------------------------------------------------------------
var useBakedImages = builder.Configuration.GetValue<bool>("USE_BAKED_IMAGES");
var registryHost = AppHostConstants.Configuration.RegistryHost;
var namespaceName = AppHostConstants.Configuration.Namespace;
var version = AppHostConstants.Configuration.Version;
var arch = AppHostConstants.Configuration.Architecture;
var envTag = AppHostConstants.Configuration.EnvironmentTag;

IResourceBuilder<IResourceWithServiceDiscovery> gatewayResource;

if (useBakedImages)
{
    var api = builder.AddContainer(AppHostConstants.Resources.Api, $"{registryHost}/{namespaceName}/api-{envTag}", $"{version}-{arch}")
        .WithHttpEndpoint(name: "http", port: AppHostConstants.Ports.Api, targetPort: 8080)
        .WithContainerRuntimeArgs("--network", networkName);
    ConfigureApi(api);

    var gateway = builder.AddContainer(AppHostConstants.Resources.Gateway, $"{registryHost}/{namespaceName}/gateway-{envTag}", $"{version}-{arch}")
        .WithHttpEndpoint(name: "http", port: AppHostConstants.Ports.Gateway, targetPort: 8080)
        .WithContainerRuntimeArgs("--network", networkName);
    ConfigureGateway(gateway);
    gatewayResource = gateway;

    var pythonAgents = builder.AddContainer(AppHostConstants.Resources.PythonAgents, $"{registryHost}/{namespaceName}/python-agents-{envTag}", $"{version}-{arch}")
        .WithHttpEndpoint(name: "http", targetPort: 8000)
        .WithContainerRuntimeArgs("--network", networkName);
    ConfigurePythonAgents(pythonAgents);
}
else
{
    var api = builder.AddProject<Projects.Aspire_Full_Api>(AppHostConstants.Resources.Api);
    ConfigureApi(api);

    var gateway = builder.AddProject<Projects.Aspire_Full_Gateway>(AppHostConstants.Resources.Gateway);
    ConfigureGateway(gateway);
    gatewayResource = gateway;

    var agents = builder.AddProject<Projects.Aspire_Full_Agents>("agents")
        .WithReference(qdrant)
        .WithReference(redis);

    var pythonAgents = builder.AddDockerfile(AppHostConstants.Resources.PythonAgents, "../../AI", "Aspire-Full.Python/python-agents/Dockerfile.agent")
        .WithHttpEndpoint(name: "http", targetPort: 8000)
        .WithContainerRuntimeArgs("--network", networkName)
        .WithExternalHttpEndpoints();
    ConfigurePythonAgents(pythonAgents);
}

// -----------------------------------------------------------------------------
// Frontend Layer - React SPA + Blazor WebAssembly
// -----------------------------------------------------------------------------
var frontend = builder.AddNpmApp(AppHostConstants.Resources.Frontend, "../../Web/Aspire-Full.Web", "dev")
    .WithHttpEndpoint(port: AppHostConstants.Ports.Frontend, targetPort: 5173, env: "VITE_PORT")
    .WithExternalHttpEndpoints()
    .WaitFor(gatewayResource);

var webAssembly = builder.AddProject<Projects.Aspire_Full_WebAssembly>(AppHostConstants.Resources.WasmDocs)
    .WithReference(gatewayResource)
    .WaitFor(gatewayResource)

void ConfigureApi<T>(IResourceBuilder<T> api) where T : IResourceWithEnvironment, IResourceWithWaitSupport
{
    api.WithReference(database)
       .WithReference(redis)
       .WaitFor(database)
       .WaitFor(redis);
}

void ConfigureGateway<T>(IResourceBuilder<T> gateway) where T : IResourceWithEnvironment, IResourceWithWaitSupport
{
    gateway.WithReference(database)
           .WithReference(qdrant)
           .WaitFor(database)
           .WaitFor(qdrant);
}

void ConfigurePythonAgents(IResourceBuilder<ContainerResource> pythonAgents)
{
    var gpuTargetUtil = aspireConfig.Telemetry.Gpu.TargetUtilization;

    pythonAgents.WithEnvironment(AppHostConstants.EnvironmentVariables.OtelServiceName, AppHostConstants.Resources.PythonAgents)
        .WithEnvironment(AppHostConstants.EnvironmentVariables.OtelExporterOtlpEndpoint, aspireConfig.Telemetry.Otlp.Endpoint)
        .WithEnvironment(AppHostConstants.EnvironmentVariables.OtelPythonLogCorrelation, "true")
        .WithEnvironment(AppHostConstants.EnvironmentVariables.CudaVisibleDevices, "0")
        .WithEnvironment(AppHostConstants.EnvironmentVariables.GpuTargetUtilization, gpuTargetUtil.ToString());

    if (aspireConfig.Agents.Gpu)
    {
        pythonAgents.WithContainerRuntimeArgs("--gpus", "all");
    }

    if (aspireConfig.Agents.Replicas > 1)
    {
        pythonAgents.WithAnnotation(new ReplicaAnnotation(aspireConfig.Agents.Replicas));
    }
}

// Build and run the distributed application
builder.Build().Run();
