using Aspire.Hosting;

// =============================================================================
// Aspire-Full Lightweight AppHost
// Back to Basics - Essential services only
// =============================================================================

var builder = DistributedApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// Data Layer - PostgreSQL + Redis + Qdrant
// -----------------------------------------------------------------------------
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume("aspire-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("aspiredb");

var redis = builder.AddRedis("redis")
    .WithRedisCommander()
    .WithDataVolume("aspire-redis-data")
    .WithLifetime(ContainerLifetime.Persistent);

var qdrant = builder.AddQdrant("qdrant")
    .WithDataVolume("aspire-qdrant-data")
    .WithLifetime(ContainerLifetime.Persistent);

// -----------------------------------------------------------------------------
// Application Services
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
// Frontend - Blazor WebAssembly (Lightweight)
// -----------------------------------------------------------------------------
var frontend = builder.AddProject<Projects.Aspire_Full_WebAssembly>("frontend")
    .WithReference(gateway)
    .WaitFor(gateway)
    .WithExternalHttpEndpoints();

// Build and run
builder.Build().Run();
