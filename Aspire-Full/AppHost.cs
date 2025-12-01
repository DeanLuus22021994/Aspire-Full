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
    .WithVolume(AppHostConstants.Volumes.DockerData, "/var/lib/docker")
    .WithVolume(AppHostConstants.Volumes.DockerCerts, "/certs")
    .WithEnvironment("DOCKER_TLS_CERTDIR", "/certs")
    .WithContainerRuntimeArgs("--host=tcp://0.0.0.0:2376", "--host=unix:///var/run/docker.sock")
    .WithContainerRuntimeArgs("--network", networkName)
    .WithHttpEndpoint(name: "engine", port: AppHostConstants.Ports.DockerEngine, targetPort: 2376)
    .WithLifetime(ContainerLifetime.Persistent);

var dockerDebugger = builder.AddContainer(AppHostConstants.Resources.DockerDebugger, dockerDebuggerImage)
    .WaitFor(dockerDaemon)
    .WithVolume(AppHostConstants.Volumes.DockerCerts, "/certs")
    .WithEnvironment("DOCKER_HOST", $"tcp://{AppHostConstants.Resources.DockerDaemon}:2376")
    .WithEnvironment("DOCKER_TLS_VERIFY", "1")
    .WithEnvironment("DOCKER_CERT_PATH", "/certs/client")
    .WithEnvironment("DOCKER_DEBUGGER_TARGET_NETWORK", networkName)
    .WithHttpEndpoint(name: "debugger-ui", port: AppHostConstants.Ports.DockerDebuggerUi, targetPort: 9393)
    .WithContainerRuntimeArgs("--network", networkName)
    .WithLifetime(ContainerLifetime.Persistent);

var dashboard = builder.AddContainer(AppHostConstants.Resources.AspireDashboard, AppHostConstants.Images.AspireDashboard)
    .WithVolume(AppHostConstants.Volumes.DashboardData, "/app/data")
    .WithEnvironment("DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS", "true")
    .WithEnvironment("DASHBOARD__OTLP__AUTHMODE", "Unsecured")
    .WithEnvironment("DASHBOARD__FRONTEND__AUTHMODE", "Unsecured")
    .WithEnvironment("DASHBOARD__RESOURCESERVICE__AUTHMODE", "Unsecured")
    .WithEnvironment("ASPIRE_DASHBOARD_MCP_ENDPOINT_URL", "http://0.0.0.0:16036")
    .WithEnvironment("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true")
    .WithHttpEndpoint(name: "ui", port: AppHostConstants.Ports.DashboardUi, targetPort: 18888)
    .WithHttpEndpoint(name: "otlp", port: AppHostConstants.Ports.DashboardOtlp, targetPort: 18889)
    .WithContainerRuntimeArgs("--network", networkName)
    .WithLifetime(ContainerLifetime.Persistent);

// -----------------------------------------------------------------------------
// Internal Docker Registry - Local artifact cache
// -----------------------------------------------------------------------------
var registry = builder.AddContainer(AppHostConstants.Resources.Registry, AppHostConstants.Images.Registry)
    .WithVolume(settings.Registry.VolumeName, "/var/lib/registry")
    .WithHttpEndpoint(name: "registry", port: settings.Registry.Port, targetPort: 5000)
    .WithContainerRuntimeArgs("--network", networkName)
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddDevContainer(networkName);

// ... (Database, Cache, Vector DB omitted) ...
// -----------------------------------------------------------------------------
// Database Layer - PostgreSQL with pgvector for semantic search
// -----------------------------------------------------------------------------
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume("aspire-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerRuntimeArgs("--network", networkName);

var database = postgres.AddDatabase("aspiredb");

// -----------------------------------------------------------------------------
// Cache Layer - Redis for session and distributed caching
// -----------------------------------------------------------------------------
var redis = builder.AddRedis("redis")
    .WithRedisCommander()
    .WithDataVolume("aspire-redis-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerRuntimeArgs("--network", networkName);

// -----------------------------------------------------------------------------
// Vector Database - Qdrant for semantic search and embeddings
// -----------------------------------------------------------------------------
var qdrant = builder.AddQdrant("qdrant")
    .WithDataVolume("aspire-qdrant-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerRuntimeArgs("--network", networkName);

// -----------------------------------------------------------------------------
// API Service - RESTful backend with Entity Framework
// -----------------------------------------------------------------------------
var useBakedImages = builder.Configuration.GetValue<bool>("USE_BAKED_IMAGES");
var registryHost = "localhost:5001";
var namespaceName = "aspire";
var version = "1.0.0";
var arch = "linux-x64";
var envTag = "dev";

if (useBakedImages)
{
    var api = builder.AddContainer("api", $"{registryHost}/{namespaceName}/api-{envTag}", $"{version}-{arch}")
        .WithHttpEndpoint(name: "http", port: 5000, targetPort: 8080)
        .WithContainerRuntimeArgs("--network", networkName);
    ConfigureApi(api);

    var gateway = builder.AddContainer("gateway", $"{registryHost}/{namespaceName}/gateway-{envTag}", $"{version}-{arch}")
        .WithHttpEndpoint(name: "http", port: 5001, targetPort: 8080)
        .WithContainerRuntimeArgs("--network", networkName);
    ConfigureGateway(gateway);

    var frontend = builder.AddContainer("frontend", $"{registryHost}/{namespaceName}/web-{envTag}", $"{version}-{arch}")
        .WithHttpEndpoint(name: "http", port: 3000, targetPort: 80)
        .WithContainerRuntimeArgs("--network", networkName);
    ConfigureFrontend(frontend, api);

    var wasmDocs = builder.AddContainer("frontend-docs", $"{registryHost}/{namespaceName}/web-assembly-{envTag}", $"{version}-{arch}")
        .WithHttpEndpoint(name: "docs", port: 5175, targetPort: 80)
        .WithContainerRuntimeArgs("--network", networkName);
    ConfigureWasmDocs(wasmDocs, api);

    var wasmUat = builder.AddContainer("frontend-uat", $"{registryHost}/{namespaceName}/web-assembly-{envTag}", $"{version}-{arch}")
        .WithHttpEndpoint(name: "uat", port: 5176, targetPort: 80)
        .WithContainerRuntimeArgs("--network", networkName);
    ConfigureWasmUat(wasmUat, api);

    var wasmProd = builder.AddContainer("frontend-prod", $"{registryHost}/{namespaceName}/web-assembly-{envTag}", $"{version}-{arch}")
        .WithHttpEndpoint(name: "prod", port: 5177, targetPort: 80)
        .WithContainerRuntimeArgs("--network", networkName);
    ConfigureWasmProd(wasmProd, api);

    var pythonAgents = builder.AddContainer("python-agents", $"{registryHost}/{namespaceName}/python-agents-{envTag}", $"{version}-{arch}")
        .WithHttpEndpoint(name: "http", port: 8000, targetPort: 8000)
        .WithContainerRuntimeArgs("--network", networkName);
    ConfigurePythonAgents(pythonAgents);
}
else
{
    var api = builder.AddProject<Projects.Aspire_Full_Api>("api");
    ConfigureApi(api);

    var gateway = builder.AddProject<Projects.Aspire_Full_Gateway>("gateway");
    ConfigureGateway(gateway);

    var frontend = builder.AddJavaScriptApp("frontend", "../Aspire-Full.Web", "dev")
        .WithHttpEndpoint(env: "PORT")
        .WithExternalHttpEndpoints();
    ConfigureFrontend(frontend, api);

    var wasmDocs = builder.AddProject<Projects.Aspire_Full_WebAssembly>("frontend-docs")
        .WithHttpEndpoint(name: "docs", port: 5175, targetPort: 5175)
        .WithExternalHttpEndpoints();
    ConfigureWasmDocs(wasmDocs, api);

    var wasmUat = builder.AddProject<Projects.Aspire_Full_WebAssembly>("frontend-uat")
        .WithHttpEndpoint(name: "uat", port: 5176, targetPort: 5176)
        .WithExternalHttpEndpoints();
    ConfigureWasmUat(wasmUat, api);

    var wasmProd = builder.AddProject<Projects.Aspire_Full_WebAssembly>("frontend-prod")
        .WithHttpEndpoint(name: "prod", port: 5177, targetPort: 5177)
        .WithExternalHttpEndpoints();
    ConfigureWasmProd(wasmProd, api);

    var pythonAgents = builder.AddDockerfile("python-agents", "..", "Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.PythonAgent")
        .WithHttpEndpoint(name: "http", port: 8000, targetPort: 8000)
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

    wasmDocs.WithEnvironment("FRONTEND_ENVIRONMENT_KEY", "docs")
            .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:5175");
}

void ConfigureWasmUat<T, U>(IResourceBuilder<T> wasmUat, IResourceBuilder<U> api)
    where T : IResourceWithEnvironment, IResourceWithWaitSupport
    where U : IResourceWithEndpoints
{
    wasmUat.WithReference(api.GetEndpoint("http"));
    ((dynamic)wasmUat).WaitFor(api);

    wasmUat.WithEnvironment("FRONTEND_ENVIRONMENT_KEY", "uat")
           .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:5176");
}

void ConfigureWasmProd<T, U>(IResourceBuilder<T> wasmProd, IResourceBuilder<U> api)
    where T : IResourceWithEnvironment, IResourceWithWaitSupport
    where U : IResourceWithEndpoints
{
    wasmProd.WithReference(api.GetEndpoint("http"));
    ((dynamic)wasmProd).WaitFor(api);

    wasmProd.WithEnvironment("FRONTEND_ENVIRONMENT_KEY", "prod")
            .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:5177");
}

void ConfigurePythonAgents(IResourceBuilder<ContainerResource> pythonAgents)
{
    pythonAgents.WithEnvironment("OTEL_SERVICE_NAME", "python-agents")
        .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://aspire-dashboard:18889")
        .WithEnvironment("OTEL_PYTHON_LOG_CORRELATION", "true")
        .WithEnvironment("CUDA_VISIBLE_DEVICES", "0")
        .WithEnvironment("GPU_TARGET_UTILIZATION", runtimeConfig.Telemetry.Gpu.Snapshot.TargetUtilization.ToString());

    if (settings.Agents.Gpu)
    {
        pythonAgents.WithContainerRuntimeArgs("--gpus", "all");
    }
}

// Build and run the distributed application
builder.Build().Run();
