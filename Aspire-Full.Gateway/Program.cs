using Aspire_Full.Embeddings;
using Aspire_Full.Embeddings.Extensions;
using Aspire_Full.Gateway.Data;
using Aspire_Full.Gateway.Endpoints;
using Aspire_Full.Gateway.Services;
using Aspire_Full.VectorStore.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
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

