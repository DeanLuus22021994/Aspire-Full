using Aspire_Full.Connectors.Embeddings;
using Aspire_Full.Connectors.DependencyInjection;
using Aspire_Full.Gateway.Data;
using Aspire_Full.Gateway.Endpoints;
using Aspire_Full.Gateway.Services;
using Aspire_Full.VectorStore.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add Tensor Core for GPU-accelerated embedding operations
builder.Services.AddTensorCore(options =>
{
    options.MaxBufferCount = 16;
    options.DefaultBufferSize = 64 * 1024 * 1024; // 64MB for embeddings
    options.PreferGpu = true;
    options.EnableMetrics = true;
});

// Add services to the container.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, Aspire_Full.Shared.AppJsonContext.Default);
});
builder.Services.AddOpenApi();

// Database
builder.AddNpgsqlDbContext<GatewayDbContext>("aspiredb", settings =>
    settings.ConnectionString = builder.Configuration.GetConnectionString("aspiredb"));

// Vector Store & Embeddings
builder.Services.AddVectorStore(builder.Configuration);
builder.Services.AddSingleton<IUserVectorService, UserVectorService>();

// Register Embedding Services
builder.Services.AddEmbeddingServices();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Create DB if not exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapUserEndpoints();

app.Run();

