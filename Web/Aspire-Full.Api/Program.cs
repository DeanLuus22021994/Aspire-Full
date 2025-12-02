using Aspire_Full.Api.Data;
using Aspire_Full.Connectors;
using Aspire_Full.DockerRegistry.Configuration;
using Aspire_Full.Tensor.Core;
using Aspire_Full.VectorStore.Extensions;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

builder.Services.AddConnectorHub(builder.Configuration);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(TensorDiagnostics.ActivitySourceName));

// Add PostgreSQL with Entity Framework
builder.AddNpgsqlDbContext<AppDbContext>("aspiredb");

// Add Redis distributed cache
builder.AddRedisDistributedCache("redis");

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, Aspire_Full.Shared.AppJsonContext.Default);
    });
builder.Services.AddOpenApi();
builder.Services.AddDockerRegistryClient(builder.Configuration);

// Add Tensor Core (GPU/CPU compute runtime with memory pool)
builder.Services.AddTensorCore(options =>
{
    options.MaxBufferCount = 32;
    options.DefaultBufferSize = 128 * 1024 * 1024; // 128MB for inference batches
    options.PreferGpu = true;
    options.EnableMetrics = true;
});

// Add Tensor Orchestration (job coordination layer)
builder.Services.AddTensorOrchestration(builder.Configuration);

// Add Vector Store
builder.Services.AddVectorStore(builder.Configuration);

// Add CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors();
app.UseAuthorization();

// Map Aspire default endpoints
app.MapDefaultEndpoints();

app.MapControllers();

await app.RunAsync();
