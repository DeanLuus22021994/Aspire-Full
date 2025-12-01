using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Aspire_Full.Tests.E2E.Fixtures;

/// <summary>
/// Provides lightweight, in-memory substitutes for the Aspire API, dashboard, OTLP, and MCP endpoints
/// so integration tests can run even when the full distributed application is unavailable.
/// </summary>
public sealed class LoopbackAspireEnvironment : IAsyncDisposable
{
    private WebApplication? _apiApp;
    private WebApplication? _dashboardApp;
    private WebApplication? _otlpApp;
    private WebApplication? _mcpApp;

    public HttpClient ApiClient { get; private set; } = null!;
    public HttpClient DashboardClient { get; private set; } = null!;
    public HttpClient OtlpClient { get; private set; } = null!;
    public HttpClient McpClient { get; private set; } = null!;

    public bool IsInitialized { get; private set; }

    public string PostgresConnectionString { get; } = "Host=loopback;Username=test;Password=test;Database=loopback";
    public string RedisConnectionString { get; } = "loopback:6379";

    public async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        _apiApp = BuildApiApp();
        _dashboardApp = BuildDashboardApp();
        _otlpApp = BuildOtlpApp();
        _mcpApp = BuildMcpApp();

        await _apiApp.StartAsync();
        await _dashboardApp.StartAsync();
        await _otlpApp.StartAsync();
        await _mcpApp.StartAsync();

        ApiClient = _apiApp.GetTestClient();
        DashboardClient = _dashboardApp.GetTestClient();
        OtlpClient = _otlpApp.GetTestClient();
        McpClient = _mcpApp.GetTestClient();

        IsInitialized = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!IsInitialized)
        {
            return;
        }

        if (_apiApp is not null)
        {
            await _apiApp.StopAsync();
            await _apiApp.DisposeAsync();
        }

        if (_dashboardApp is not null)
        {
            await _dashboardApp.StopAsync();
            await _dashboardApp.DisposeAsync();
        }

        if (_otlpApp is not null)
        {
            await _otlpApp.StopAsync();
            await _otlpApp.DisposeAsync();
        }

        if (_mcpApp is not null)
        {
            await _mcpApp.StopAsync();
            await _mcpApp.DisposeAsync();
        }

        ApiClient.Dispose();
        DashboardClient.Dispose();
        OtlpClient.Dispose();
        McpClient.Dispose();

        IsInitialized = false;
    }

    private static WebApplication BuildApiApp()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();

        var app = builder.Build();
        var items = new ConcurrentDictionary<int, ItemDto>();
        var idSeed = 0;

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/alive", () => Results.Ok(new { status = "alive" }));
        app.MapGet("/openapi/v1.json", () => Results.Text("{\"openapi\":\"3.0.0\"}", "application/json"));

        app.MapGet("/api/items", () => Results.Json(items.Values));

        app.MapGet("/api/items/{id:int}", (int id) =>
            items.TryGetValue(id, out var item)
                ? Results.Json(item)
                : Results.NotFound());

        app.MapPost("/api/items", (ItemInput input) =>
        {
            var id = Interlocked.Increment(ref idSeed);
            var dto = new ItemDto(id, input.Name, input.Description, DateTime.UtcNow, DateTime.UtcNow);
            items[id] = dto;
            return Results.Created($"/api/items/{id}", dto);
        });

        app.MapPut("/api/items/{id:int}", (int id, ItemInput input) =>
        {
            if (!items.TryGetValue(id, out var existing))
            {
                return Results.NotFound();
            }

            var updated = existing with { Name = input.Name ?? existing.Name, Description = input.Description ?? existing.Description, UpdatedAt = DateTime.UtcNow };
            items[id] = updated;
            return Results.Json(updated);
        });

        app.MapDelete("/api/items/{id:int}", (int id) =>
            items.TryRemove(id, out _)
                ? Results.NoContent()
                : Results.NotFound());

        return app;
    }

    private static WebApplication BuildDashboardApp()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        app.MapGet("/", () => Results.Text("<html><body>Dashboard</body></html>", "text/html"));
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        return app;
    }

    private static WebApplication BuildOtlpApp()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        app.MapGet("/", () => Results.Ok());
        app.MapPost("/v1/traces", () => Results.Ok());
        app.MapPost("/v1/logs", () => Results.Ok());

        return app;
    }

    private static WebApplication BuildMcpApp()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        app.MapGet("/", () => Results.Ok(new { status = "ok" }));
        app.MapPost("/", async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            var payload = new { jsonrpc = "2.0", id = 1, result = new { echoed = body.Length } };
            await context.Response.WriteAsJsonAsync(payload);
        });

        return app;
    }

    private sealed record ItemInput(string Name, string? Description);

    private sealed record ItemDto(int Id, string Name, string? Description, DateTime CreatedAt, DateTime UpdatedAt);
}
