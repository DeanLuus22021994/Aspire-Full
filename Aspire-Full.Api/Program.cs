using Aspire_Full.Api.Data;
using Aspire_Full.DockerRegistry;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add PostgreSQL with Entity Framework
builder.AddNpgsqlDbContext<AppDbContext>("aspiredb");

// Add Redis distributed cache
builder.AddRedisDistributedCache("redis");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDockerRegistryClient(builder.Configuration);

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
