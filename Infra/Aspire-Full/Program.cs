using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Qdrant;
using Aspire_Full.Configuration;
using Aspire_Full.DevContainer;
using Microsoft.Extensions.Configuration;

// =============================================================================
// Aspire Full AppHost - Distributed Application Orchestrator
// =============================================================================
// ... (header omitted) ...

var builder = DistributedApplication.CreateBuilder(args);

// Load Configuration
var settings = ConfigLoader.LoadSettings(".aspire/settings.json");
var runtimeConfig = ConfigLoader.LoadRuntimeConfig(".config/config.yaml");

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

var dashboard = builder.AddContainer(AppHostConstants.Resources.AspireDashboard, AppHostConstants.Images.AspireDashboard)
    .WithVolume(AppHostConstants.Volumes.DashboardData, AppHostConstants.Paths.DashboardData)
    .WithEnvironment(AppHostConstants.EnvironmentVariables.DotnetDashboardUnsecuredAllowAnonymous, "true")
    .WithEnvironment(AppHostConstants.EnvironmentVariables.DashboardOtlpAuthMode, "Unsecured")
    .WithEnvironment(AppHostConstants.EnvironmentVariables.DashboardFrontendAuthMode, "Unsecured")
    .WithEnvironment(AppHostConstants.EnvironmentVariables.DashboardResourceServiceAuthMode, "Unsecured")
    .WithEnvironment(AppHostConstants.EnvironmentVariables.AspireDashboardMcpEndpointUrl, "http://0.0.0.0:16036")
    .WithEnvironment(AppHostConstants.EnvironmentVariables.AspireAllowUnsecuredTransport, "true")
    .WithHttpEndpoint(name: "ui", port: AppHostConstants.Ports.DashboardUi, targetPort: 18888)
    .WithHttpEndpoint(name: "otlp", port: AppHostConstants.Ports.DashboardOtlp, targetPort: 18889)
    .WithContainerRuntimeArgs("--network", networkName)
    .WithLifetime(ContainerLifetime.Persistent);

// -----------------------------------------------------------------------------
// Internal Docker Registry - Local artifact cache
// -----------------------------------------------------------------------------
var registry = builder.AddContainer(AppHostConstants.Resources.Registry, AppHostConstants.Images.Registry)
    .WithVolume(settings.Registry.VolumeName, AppHostConstants.Paths.RegistryData)
    .WithHttpEndpoint(name: "registry", port: settings.Registry.Port, targetPort: 5000)
    .WithContainerRuntimeArgs("--network", networkName)
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddDevContainer(networkName);

// ... (Database, Cache, Vector DB omitted) ...
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
// API Service - RESTful backend with Entity Framework
// -----------------------------------------------------------------------------
var useBakedImages = builder.Configuration.GetValue<bool>("USE_BAKED_IMAGES");
var registryHost = AppHostConstants.Configuration.RegistryHost;
var namespaceName = AppHostConstants.Configuration.Namespace;
var version = AppHostConstants.Configuration.Version;
var arch = AppHostConstants.Configuration.Architecture;
var envTag = AppHostConstants.Configuration.EnvironmentTag;

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

    //var frontend = builder.AddContainer(AppHostConstants.Resources.Frontend, $"{registryHost}/{namespaceName}/web-{envTag}", $"{version}-{arch}")
    //    .WithHttpEndpoint(name: "http", port: AppHostConstants.Ports.Frontend, targetPort: 80)
    //    .WithContainerRuntimeArgs("--network", networkName);
    //ConfigureFrontend(frontend, api);

    //var wasmDocs = builder.AddContainer(AppHostConstants.Resources.WasmDocs, $"{registryHost}/{namespaceName}/web-assembly-{envTag}", $"{version}-{arch}")
    //    .WithHttpEndpoint(name: "docs", port: AppHostConstants.Ports.WasmDocs, targetPort: 80)
    //    .WithContainerRuntimeArgs("--network", networkName);
    //ConfigureWasmDocs(wasmDocs, api);

    //var wasmUat = builder.AddContainer(AppHostConstants.Resources.WasmUat, $"{registryHost}/{namespaceName}/web-assembly-{envTag}", $"{version}-{arch}")
    //    .WithHttpEndpoint(name: "uat", port: AppHostConstants.Ports.WasmUat, targetPort: 80)
    //    .WithContainerRuntimeArgs("--network", networkName);
    //ConfigureWasmUat(wasmUat, api);

    //var wasmProd = builder.AddContainer(AppHostConstants.Resources.WasmProd, $"{registryHost}/{namespaceName}/web-assembly-{envTag}", $"{version}-{arch}")
    //    .WithHttpEndpoint(name: "prod", port: AppHostConstants.Ports.WasmProd, targetPort: 80)
    //    .WithContainerRuntimeArgs("--network", networkName);
    //ConfigureWasmProd(wasmProd, api);

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

    //var frontend = builder.AddJavaScriptApp(AppHostConstants.Resources.Frontend, "../../Web/Aspire-Full.Web", "dev")
    //    .WithHttpEndpoint(env: "PORT")
    //    .WithExternalHttpEndpoints();
    //ConfigureFrontend(frontend, api);

    //var wasmDocs = builder.AddProject<Projects.Aspire_Full_WebAssembly>(AppHostConstants.Resources.WasmDocs)
    //    .WithHttpEndpoint(name: "docs", port: AppHostConstants.Ports.WasmDocs, targetPort: 5175)
    //    .WithExternalHttpEndpoints();
    //ConfigureWasmDocs(wasmDocs, api);

    //var wasmUat = builder.AddProject<Projects.Aspire_Full_WebAssembly>(AppHostConstants.Resources.WasmUat)
    //    .WithHttpEndpoint(name: "uat", port: AppHostConstants.Ports.WasmUat, targetPort: 5176)
    //    .WithExternalHttpEndpoints();
    //ConfigureWasmUat(wasmUat, api);

    //var wasmProd = builder.AddProject<Projects.Aspire_Full_WebAssembly>(AppHostConstants.Resources.WasmProd)
    //    .WithHttpEndpoint(name: "prod", port: AppHostConstants.Ports.WasmProd, targetPort: 5177)
    //    .WithExternalHttpEndpoints();
    //ConfigureWasmProd(wasmProd, api);

    var agents = builder.AddProject<Projects.Aspire_Full_Agents>("agents")
        .WithReference(qdrant)
        .WithReference(redis);

    var pythonAgents = builder.AddDockerfile(AppHostConstants.Resources.PythonAgents, "../../AI", "Aspire-Full.Python/python-agents/Dockerfile.agent")
        .WithHttpEndpoint(name: "http", targetPort: 8000)
        .WithContainerRuntimeArgs("--network", networkName)
        .WithExternalHttpEndpoints();
    ConfigurePythonAgents(pythonAgents);
}

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

void ConfigureFrontend<T, U>(IResourceBuilder<T> frontend, IResourceBuilder<U> api)
    where T : IResourceWithEnvironment, IResourceWithWaitSupport
    where U : IResourceWithEndpoints
{
    frontend.WithReference(api.GetEndpoint("http"));
    ((dynamic)frontend).WaitFor(api);
}

void ConfigureWasmDocs<T, U>(IResourceBuilder<T> wasmDocs, IResourceBuilder<U> api)
    where T : IResourceWithEnvironment, IResourceWithWaitSupport
    where U : IResourceWithEndpoints
{
    wasmDocs.WithReference(api.GetEndpoint("http"));
    ((dynamic)wasmDocs).WaitFor(api);

    wasmDocs.WithEnvironment(AppHostConstants.EnvironmentVariables.FrontendEnvironmentKey, "docs")
            .WithEnvironment(AppHostConstants.EnvironmentVariables.AspNetCoreUrls, "http://0.0.0.0:5175");
}

void ConfigureWasmUat<T, U>(IResourceBuilder<T> wasmUat, IResourceBuilder<U> api)
    where T : IResourceWithEnvironment, IResourceWithWaitSupport
    where U : IResourceWithEndpoints
{
    wasmUat.WithReference(api.GetEndpoint("http"));
    ((dynamic)wasmUat).WaitFor(api);

    wasmUat.WithEnvironment(AppHostConstants.EnvironmentVariables.FrontendEnvironmentKey, "uat")
           .WithEnvironment(AppHostConstants.EnvironmentVariables.AspNetCoreUrls, "http://0.0.0.0:5176");
}

void ConfigureWasmProd<T, U>(IResourceBuilder<T> wasmProd, IResourceBuilder<U> api)
    where T : IResourceWithEnvironment, IResourceWithWaitSupport
    where U : IResourceWithEndpoints
{
    wasmProd.WithReference(api.GetEndpoint("http"));
    ((dynamic)wasmProd).WaitFor(api);

    wasmProd.WithEnvironment(AppHostConstants.EnvironmentVariables.FrontendEnvironmentKey, "prod")
            .WithEnvironment(AppHostConstants.EnvironmentVariables.AspNetCoreUrls, "http://0.0.0.0:5177");
}

void ConfigurePythonAgents(IResourceBuilder<ContainerResource> pythonAgents)
{
    pythonAgents.WithEnvironment(AppHostConstants.EnvironmentVariables.OtelServiceName, AppHostConstants.Resources.PythonAgents)
        .WithEnvironment(AppHostConstants.EnvironmentVariables.OtelExporterOtlpEndpoint, "http://aspire-dashboard:18889")
        .WithEnvironment(AppHostConstants.EnvironmentVariables.OtelPythonLogCorrelation, "true")
        .WithEnvironment(AppHostConstants.EnvironmentVariables.CudaVisibleDevices, "0")
        .WithEnvironment(AppHostConstants.EnvironmentVariables.GpuTargetUtilization, runtimeConfig.Telemetry.Gpu.Snapshot.TargetUtilization.ToString());

    if (settings.Agents.Gpu)
    {
        pythonAgents.WithContainerRuntimeArgs("--gpus", "all");
    }

    if (settings.Agents.Replicas > 1)
    {
        pythonAgents.WithAnnotation(new ReplicaAnnotation(settings.Agents.Replicas));
    }
}

// Build and run the distributed application
builder.Build().Run();
