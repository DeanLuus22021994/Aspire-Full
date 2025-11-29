using Aspire.Hosting;
using Aspire.Hosting.Qdrant;
using Aspire_Full.DevContainer;

// =============================================================================
// Aspire Full AppHost - Distributed Application Orchestrator
// =============================================================================
// This is the main entry point for the .NET Aspire application.
// It configures and orchestrates all services in the distributed application.
//
// Architecture:
//   - Aspire manages its own containers (PostgreSQL, Redis, Qdrant, devcontainer, admin UIs)
//   - All containers join aspire-network for unified low-latency communication
//   - Telemetry is sent to internal dashboard via OTLP (port 18889)
//
// Features:
//   - Service discovery and configuration
//   - Container orchestration with shared networking
//   - OpenTelemetry integration with standalone dashboard
//   - Health checks and monitoring
//   - GPU acceleration support (NVIDIA CUDA/TensorRT)
//
// Environment Variables:
//   - DOTNET_DASHBOARD_OTLP_ENDPOINT_URL - OTLP endpoint for telemetry
//   - ASPIRE_ALLOW_UNSECURED_TRANSPORT - Allow HTTP for development
//   - OTEL_EXPORTER_OTLP_ENDPOINT - OpenTelemetry endpoint
//   - CUDA_VISIBLE_DEVICES - GPU device selection
//
// Usage:
//   dotnet run --project Aspire-Full --launch-profile headless
//   ./scripts/Start-Aspire.ps1
// =============================================================================

var builder = DistributedApplication.CreateBuilder(args);

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

builder.AddDevContainer(networkName);

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
var pythonAgents = builder.AddExecutable("python-agents", "uv", "../Aspire-Full.Python/python-agents", "run", "--with-group", "tracing", "python", "src/aspire_agents/examples/realtime/app/server.py")
    .WithEnvironment("OTEL_SERVICE_NAME", "python-agents")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:18889") // Use localhost for host process, or dashboard service name if in container
    .WithHttpEndpoint(name: "http", port: 8000, targetPort: 8000)
    .WithExternalHttpEndpoints();

// Build and run the distributed application
builder.Build().Run();
