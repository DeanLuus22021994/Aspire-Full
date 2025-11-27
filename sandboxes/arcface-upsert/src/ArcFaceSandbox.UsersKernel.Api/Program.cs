using System.Text.Json.Serialization;
using ArcFaceSandbox.EmbeddingService;
using ArcFaceSandbox.UsersKernel.Infrastructure.Data;
using ArcFaceSandbox.UsersKernel.Infrastructure.Services;
using ArcFaceSandbox.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

const string SandboxUiCorsPolicy = "SandboxUi";

builder.Services.AddArcFaceEmbedding(builder.Configuration);
builder.Services.AddSandboxVectorStore(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy(SandboxUiCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("SandboxUi:AllowedOrigins")
            .Get<string[]>();

        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.AddDbContext<SandboxUsersDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Users")
        ?? "Data Source=arcface-sandbox-users.db";
    options.UseSqlite(connectionString);
});

builder.Services.AddScoped<ISandboxUserService, SandboxUserService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SandboxUsersDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(SandboxUiCorsPolicy);
app.MapControllers();

app.Run();

public partial class Program;
