using Aspire.Hosting.Qdrant;

// =============================================================================
// Aspire Full AppHost - Distributed Application Orchestrator
// =============================================================================
// This is the main entry point for the .NET Aspire application.
// It configures and orchestrates all services in the distributed application.
//
// Architecture:
//   - Aspire manages its own containers (PostgreSQL, Redis, Qdrant, admin UIs)
//   - All containers join aspire-network for unified low-latency communication
//   - Telemetry is sent to external dashboard via OTLP (port 18889)
//   - Docker Compose manages: devcontainer, aspire-dashboard
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
