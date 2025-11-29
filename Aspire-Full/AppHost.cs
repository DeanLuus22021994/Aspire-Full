using Aspire.Hosting;
using Aspire.Hosting.Qdrant;
using Aspire_Full.Configuration;
using Aspire_Full.DevContainer;

// =============================================================================
// Aspire Full AppHost - Distributed Application Orchestrator
// =============================================================================
// ... (header omitted) ...

var builder = DistributedApplication.CreateBuilder(args);

// Load Configuration
var settings = ConfigLoader.LoadSettings(".aspire/settings.json");
var runtimeConfig = ConfigLoader.LoadRuntimeConfig(".config/config.yaml");

// External network for container-to-container communication
const string networkName = "aspire-network";
const string dockerDebuggerImage = "docker/debugger:latest";

// -----------------------------------------------------------------------------
// Dev Infrastructure - Docker-in-Docker daemon + dashboard + devcontainer
// -----------------------------------------------------------------------------
var dockerDaemon = builder.AddContainer("docker", "docker:27-dind")
    .WithVolume("aspire-docker-data", "/var/lib/docker")
    .WithVolume("aspire-docker-certs", "/certs")
    .WithEnvironment("DOCKER_TLS_CERTDIR", "/certs")
    .WithContainerRuntimeArgs("--host=tcp://0.0.0.0:2376", "--host=unix:///var/run/docker.sock")
    .WithContainerRuntimeArgs("--network", networkName)
    .WithHttpEndpoint(name: "engine", port: 2376, targetPort: 2376)
    .WithLifetime(ContainerLifetime.Persistent);

var dockerDebugger = builder.AddContainer("docker-debugger", dockerDebuggerImage)
    .WaitFor(dockerDaemon)
    .WithVolume("aspire-docker-certs", "/certs")
    .WithEnvironment("DOCKER_HOST", "tcp://docker:2376")
    .WithEnvironment("DOCKER_TLS_VERIFY", "1")
    .WithEnvironment("DOCKER_CERT_PATH", "/certs/client")
    .WithEnvironment("DOCKER_DEBUGGER_TARGET_NETWORK", networkName)
    .WithHttpEndpoint(name: "debugger-ui", port: 9393, targetPort: 9393)
    .WithContainerRuntimeArgs("--network", networkName)
    .WithLifetime(ContainerLifetime.Persistent);

var dashboard = builder.AddContainer("aspire-dashboard", "mcr.microsoft.com/dotnet/aspire-dashboard:latest")
    .WithVolume("aspire-dashboard-data", "/app/data")
    .WithEnvironment("DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS", "true")
    .WithEnvironment("DASHBOARD__OTLP__AUTHMODE", "Unsecured")
    .WithEnvironment("DASHBOARD__FRONTEND__AUTHMODE", "Unsecured")
    .WithEnvironment("DASHBOARD__RESOURCESERVICE__AUTHMODE", "Unsecured")
    .WithEnvironment("ASPIRE_DASHBOARD_MCP_ENDPOINT_URL", "http://0.0.0.0:16036")
    .WithEnvironment("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true")
    .WithHttpEndpoint(name: "ui", port: 18888, targetPort: 18888)
    .WithHttpEndpoint(name: "otlp", port: 18889, targetPort: 18889)
    .WithContainerRuntimeArgs("--network", networkName)
    .WithLifetime(ContainerLifetime.Persistent);

// -----------------------------------------------------------------------------
// Internal Docker Registry - Local artifact cache
// -----------------------------------------------------------------------------
var registry = builder.AddContainer("registry", "registry:2")
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
var api = builder.AddProject<Projects.Aspire_Full_Api>("api")
    .WithReference(database)
    .WithReference(redis)
    .WaitFor(database)
    .WaitFor(redis);

var gateway = builder.AddProject<Projects.Aspire_Full_Gateway>("gateway")
    .WithReference(database)
    .WithReference(qdrant)
    .WaitFor(database)
    .WaitFor(qdrant);

// -----------------------------------------------------------------------------
// Web Frontend - Semantic UI React application
// -----------------------------------------------------------------------------
var frontend = builder.AddJavaScriptApp("frontend", "../Aspire-Full.Web", "dev")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

var wasmDocs = builder.AddProject<Projects.Aspire_Full_WebAssembly>("frontend-docs")
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("FRONTEND_ENVIRONMENT_KEY", "docs")
    .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:5175")
    .WithHttpEndpoint(name: "docs", port: 5175, targetPort: 5175)
    .WithExternalHttpEndpoints();

var wasmUat = builder.AddProject<Projects.Aspire_Full_WebAssembly>("frontend-uat")
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("FRONTEND_ENVIRONMENT_KEY", "uat")
    .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:5176")
    .WithHttpEndpoint(name: "uat", port: 5176, targetPort: 5176)
    .WithExternalHttpEndpoints();

var wasmProd = builder.AddProject<Projects.Aspire_Full_WebAssembly>("frontend-prod")
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("FRONTEND_ENVIRONMENT_KEY", "prod")
    .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:5177")
    .WithHttpEndpoint(name: "prod", port: 5177, targetPort: 5177)
    .WithExternalHttpEndpoints();

// -----------------------------------------------------------------------------
// Python Agents - Realtime API
// -----------------------------------------------------------------------------
// Automated build using Dockerfile.agent
var pythonAgents = builder.AddDockerfile("python-agents", "../Aspire-Full.Python/python-agents", "Dockerfile.agent")
    .WithEnvironment("OTEL_SERVICE_NAME", "python-agents")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://aspire-dashboard:18889")
    .WithEnvironment("OTEL_PYTHON_LOG_CORRELATION", "true")
    .WithEnvironment("CUDA_VISIBLE_DEVICES", "0")
    .WithEnvironment("GPU_TARGET_UTILIZATION", runtimeConfig.Telemetry.Gpu.Snapshot.TargetUtilization.ToString())
    .WithHttpEndpoint(name: "http", port: 8000, targetPort: 8000)
    .WithContainerRuntimeArgs("--network", networkName)
    .WithExternalHttpEndpoints();

if (settings.Agents.Gpu)
{
    pythonAgents.WithContainerRuntimeArgs("--gpus", "all");
}

// Build and run the distributed application
builder.Build().Run();
