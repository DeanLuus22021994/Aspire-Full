using Aspire.Hosting;
using Aspire.Hosting.Qdrant;

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

// -----------------------------------------------------------------------------
// Dev Infrastructure - Docker-in-Docker daemon + dashboard + devcontainer
// -----------------------------------------------------------------------------
var dockerDaemon = builder.AddContainer("docker", "docker:27-dind")
    .WithVolume("aspire-docker-data", "/var/lib/docker")
    .WithVolume("aspire-docker-certs", "/certs")
    .WithEnvironment("DOCKER_TLS_CERTDIR", "/certs")
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
    .WithHttpEndpoint(name: "mcp", port: 16036, targetPort: 16036)
    .WithContainerRuntimeArgs("--network", networkName)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithReference(dockerDaemon);

var devcontainer = builder.AddDockerfile("devcontainer", "../.devcontainer")
    .WithVolume("aspire-nuget-cache", "/home/vscode/.nuget")
    .WithVolume("aspire-dotnet-tools", "/home/vscode/.dotnet/tools")
    .WithVolume("aspire-aspire-cli", "/home/vscode/.aspire")
    .WithVolume("aspire-vscode-extensions", "/home/vscode/.vscode-server/extensions")
    .WithVolume("aspire-workspace", "/workspace")
    .WithVolume("aspire-docker-certs", "/certs")
    .WithEnvironment("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
    .WithEnvironment("DOTNET_NOLOGO", "1")
    .WithEnvironment("DOTNET_RUNNING_IN_CONTAINER", "true")
    .WithEnvironment("NUGET_PACKAGES", "/home/vscode/.nuget/packages")
    .WithEnvironment("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL", "http://aspire-dashboard:18889")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://aspire-dashboard:18889")
    .WithEnvironment("ASPIRE_DASHBOARD_MCP_ENDPOINT_URL", "http://aspire-dashboard:16036")
    .WithEnvironment("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true")
    .WithEnvironment("DOCKER_HOST", "tcp://docker:2376")
    .WithEnvironment("DOCKER_TLS_VERIFY", "1")
    .WithEnvironment("DOCKER_CERT_PATH", "/certs/client")
    .WithEnvironment("NVIDIA_VISIBLE_DEVICES", "all")
    .WithEnvironment("NVIDIA_DRIVER_CAPABILITIES", "compute,utility")
    .WithEnvironment("NVIDIA_REQUIRE_CUDA", "cuda>=12.4,driver>=535")
    .WithArgs("sleep", "infinity")
    .WithContainerRuntimeArgs("--network", networkName, "--gpus", "all", "--init")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithReference(dockerDaemon)
    .WithReference(dashboard);

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

// -----------------------------------------------------------------------------
// Web Frontend - Semantic UI React application
// -----------------------------------------------------------------------------
var frontend = builder.AddJavaScriptApp("frontend", "../Aspire-Full.Web", "dev")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

// Build and run the distributed application
builder.Build().Run();
