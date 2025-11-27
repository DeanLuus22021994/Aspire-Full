// =============================================================================
// Aspire Full AppHost - Distributed Application Orchestrator
// =============================================================================
// This is the main entry point for the .NET Aspire application.
// It configures and orchestrates all services in the distributed application.
//
// Architecture:
//   - Aspire manages its own containers (PostgreSQL, Redis, admin UIs)
//   - Aspire creates an isolated session network for container communication
//   - Telemetry is sent to external dashboard via OTLP (port 18889)
//   - Docker Compose manages: devcontainer, aspire-dashboard
//
// Features:
//   - Service discovery and configuration
//   - Container orchestration with isolated networking
//   - OpenTelemetry integration with standalone dashboard
//   - Health checks and monitoring
//
// Environment Variables:
//   - DOTNET_DASHBOARD_OTLP_ENDPOINT_URL - OTLP endpoint for telemetry
//   - ASPIRE_ALLOW_UNSECURED_TRANSPORT - Allow HTTP for development
//   - OTEL_EXPORTER_OTLP_ENDPOINT - OpenTelemetry endpoint
//
// Usage:
//   dotnet run --project Aspire-Full
//   aspire run
// =============================================================================

var builder = DistributedApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// Database Layer - PostgreSQL with pgvector for semantic search
// -----------------------------------------------------------------------------
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume("aspire-postgres-data");

var database = postgres.AddDatabase("aspiredb");

// -----------------------------------------------------------------------------
// Cache Layer - Redis for session and distributed caching
// -----------------------------------------------------------------------------
var redis = builder.AddRedis("redis")
    .WithRedisCommander()
    .WithDataVolume("aspire-redis-data");

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
