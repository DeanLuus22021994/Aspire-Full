using Aspire_Full.Gateway.Data;
using Aspire_Full.Gateway.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, Aspire_Full.Shared.AppJsonContext.Default);
});
builder.Services.AddOpenApi();

// Database
builder.AddNpgsqlDbContext<GatewayDbContext>("aspiredb");

// Qdrant vector database
builder.AddQdrantClient("qdrant");

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline
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

