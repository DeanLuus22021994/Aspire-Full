using Aspire_Full.Api.Data;
using Aspire_Full.Api.Tensor;
using Aspire_Full.DockerRegistry;
using Aspire_Full.Qdrant;
using Aspire_Full.Tensor;
using Aspire_Full.VectorStore;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(TensorDiagnostics.ActivitySourceName));

// Add PostgreSQL with Entity Framework
builder.AddNpgsqlDbContext<AppDbContext>("aspiredb");

// Add Redis distributed cache
builder.AddRedisDistributedCache("redis");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDockerRegistryClient(builder.Configuration);
builder.Services.AddOptions<TensorModelCatalogOptions>()
    .Bind(builder.Configuration.GetSection("TensorModels"))
    .ValidateOnStart();
builder.Services.AddSingleton<ITensorJobStore, InMemoryTensorJobStore>();
builder.Services.AddSingleton<ITensorVectorBridge, TensorVectorBridge>();
builder.Services.AddSingleton<ITensorJobCoordinator, TensorJobCoordinator>();
builder.Services.AddQdrantClient(builder.Configuration);
builder.Services.AddSingleton<IVectorStoreService, QdrantVectorStoreService>();

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
