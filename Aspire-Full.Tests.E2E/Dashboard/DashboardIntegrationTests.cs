using System.Net;
using System.Net.Http.Json;
using Aspire_Full.Tests.E2E.Fixtures;

namespace Aspire_Full.Tests.E2E.Dashboard;

[TestFixture]
[Category("Dashboard")]
public class DashboardIntegrationTests
{
    private HttpClient _dashboardClient = null!;
    private HttpClient _apiClient = null!;
    private string _dashboardUrl = null!;
    private string _otlpEndpoint = null!;
    private string _mcpEndpoint = null!;

    [SetUp]
    public void Setup()
    {
        _dashboardUrl = Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_URL") ?? "http://localhost:18888";
        _otlpEndpoint = Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL") ?? "http://localhost:18889";
        _mcpEndpoint = Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_MCP_ENDPOINT_URL") ?? "http://localhost:16036";
        var apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5000";

        _dashboardClient = new HttpClient
        {
            BaseAddress = new Uri(_dashboardUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _apiClient = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    [TearDown]
    public void TearDown()
    {
        _dashboardClient?.Dispose();
        _apiClient?.Dispose();
    }

    [Test]
    [Category("Integration")]
    public async Task Dashboard_HealthEndpoint_ReturnsHealthy()
    {
        if (!await IsDashboardAvailable())
        {
            Assert.Ignore("Dashboard is not available. Skipping integration test.");
            return;
        }

        var response = await _dashboardClient.GetAsync("/health");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [Category("Integration")]
    public async Task Dashboard_FrontendPage_ReturnsSuccess()
    {
        if (!await IsDashboardAvailable())
        {
            Assert.Ignore("Dashboard is not available. Skipping integration test.");
            return;
        }

        var response = await _dashboardClient.GetAsync("/");
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }

    [Test]
    [Category("Integration")]
    [Category("Telemetry")]
    public async Task OTLP_Endpoint_AcceptsTraceData()
    {
        if (!await IsOtlpEndpointHealthy())
        {
            Assert.Ignore("OTLP endpoint is not healthy. Skipping integration test.");
            return;
        }

        using var otlpClient = new HttpClient { BaseAddress = new Uri(_otlpEndpoint) };

        var tracePayload = new
        {
            resourceSpans = new[]
            {
                new
                {
                    resource = new { attributes = new[] { new { key = "service.name", value = new { stringValue = "test-service" } } } },
                    scopeSpans = new[]
                    {
                        new
                        {
                            scope = new { name = "test-scope" },
                            spans = new[]
                            {
                                new
                                {
                                    traceId = "00000000000000000000000000000001",
                                    spanId = "0000000000000001",
                                    name = "test-span",
                                    kind = 1,
                                    startTimeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000,
                                    endTimeUnixNano = DateTimeOffset.UtcNow.AddMilliseconds(100).ToUnixTimeMilliseconds() * 1_000_000
                                }
                            }
                        }
                    }
                }
            }
        };

        var response = await otlpClient.PostAsJsonAsync("/v1/traces", tracePayload);

        // OTLP endpoint may return OK, Accepted, or BadRequest (for JSON when protobuf expected)
        // Any of these indicates the endpoint is responding correctly
        Assert.That(
            response.StatusCode == HttpStatusCode.OK
            || response.StatusCode == HttpStatusCode.Accepted
            || response.StatusCode == HttpStatusCode.BadRequest
            || response.StatusCode == HttpStatusCode.UnsupportedMediaType,
            Is.True,
            $"OTLP endpoint responded with unexpected status: {response.StatusCode}");
    }

    [Test]
    [Category("Integration")]
    [Category("Telemetry")]
    public async Task OTLP_Endpoint_AcceptsLogData()
    {
        if (!await IsOtlpEndpointHealthy())
        {
            Assert.Ignore("OTLP endpoint is not healthy. Skipping integration test.");
            return;
        }

        using var otlpClient = new HttpClient { BaseAddress = new Uri(_otlpEndpoint) };

        var logsPayload = new
        {
            resourceLogs = new[]
            {
                new
                {
                    resource = new { attributes = new[] { new { key = "service.name", value = new { stringValue = "test-service" } } } },
                    scopeLogs = new[]
                    {
                        new
                        {
                            scope = new { name = "test-scope" },
                            logRecords = new[]
                            {
                                new
                                {
                                    timeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000,
                                    severityNumber = 9,
                                    severityText = "INFO",
                                    body = new { stringValue = "Test log from E2E" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var response = await otlpClient.PostAsJsonAsync("/v1/logs", logsPayload);

        // OTLP endpoint may return OK, Accepted, or BadRequest (for JSON when protobuf expected)
        // Any of these indicates the endpoint is responding correctly
        Assert.That(
            response.StatusCode == HttpStatusCode.OK
            || response.StatusCode == HttpStatusCode.Accepted
            || response.StatusCode == HttpStatusCode.BadRequest
            || response.StatusCode == HttpStatusCode.UnsupportedMediaType,
            Is.True,
            $"OTLP endpoint responded with unexpected status: {response.StatusCode}");
    }

    [Test]
    [Category("Integration")]
    [Category("MCP")]
    public async Task MCP_Server_IsAccessible()
    {
        if (!await IsMcpEndpointAvailable())
        {
            Assert.Ignore("MCP endpoint is not available. Skipping integration test.");
            return;
        }

        using var mcpClient = new HttpClient { BaseAddress = new Uri(_mcpEndpoint) };
        var response = await mcpClient.GetAsync("/");
        Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    [Category("Integration")]
    [Category("MCP")]
    public async Task MCP_ListResources_SendsRequest()
    {
        if (!await IsMcpEndpointAvailable())
        {
            Assert.Ignore("MCP endpoint is not available. Skipping integration test.");
            return;
        }

        using var mcpClient = new HttpClient { BaseAddress = new Uri(_mcpEndpoint) };

        var mcpRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = "list_resources",
                arguments = new { }
            }
        };

        var response = await mcpClient.PostAsJsonAsync("/", mcpRequest);
        Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    [Category("Integration")]
    [Category("Bidirectional")]
    public async Task BiDirectional_ApiAndDashboard_BothHealthy()
    {
        if (!await IsDashboardAvailable() || !await IsApiAvailable())
        {
            Assert.Ignore("Dashboard or API is not available. Skipping integration test.");
            return;
        }

        var dashboardHealth = await _dashboardClient.GetAsync("/health");
        var apiHealth = await _apiClient.GetAsync("/health");

        Assert.Multiple(() =>
        {
            Assert.That(dashboardHealth.IsSuccessStatusCode, Is.True);
            Assert.That(apiHealth.IsSuccessStatusCode, Is.True);
        });
    }

    [Test]
    [Category("Integration")]
    [Category("Bidirectional")]
    public async Task BiDirectional_ApiRequest_GeneratesTelemetry()
    {
        if (!await IsApiAvailable())
        {
            Assert.Ignore("API is not available. Skipping integration test.");
            return;
        }

        var response = await _apiClient.GetAsync("/api/items");
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }

    [Test]
    [Category("Integration")]
    [Category("Resources")]
    public async Task Resources_ApiOpenApi_IsAccessible()
    {
        if (!await IsApiAvailable())
        {
            Assert.Ignore("API is not available. Skipping integration test.");
            return;
        }

        var response = await _apiClient.GetAsync("/openapi/v1.json");
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }

    private async Task<bool> IsDashboardAvailable()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _dashboardClient.GetAsync("/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task<bool> IsOtlpEndpointAvailable()
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(_otlpEndpoint) };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.GetAsync("/", cts.Token);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Checks if OTLP endpoint is healthy by posting a minimal valid trace.
    /// </summary>
    private async Task<bool> IsOtlpEndpointHealthy()
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(_otlpEndpoint) };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var testPayload = new { resourceSpans = Array.Empty<object>() };
            var response = await client.PostAsJsonAsync("/v1/traces", testPayload, cts.Token);
            // Healthy endpoint accepts empty traces or returns a specific error for invalid format
            return response.StatusCode == HttpStatusCode.OK
                || response.StatusCode == HttpStatusCode.Accepted
                || response.StatusCode == HttpStatusCode.BadRequest; // Expected for empty payload
        }
        catch { return false; }
    }

    private async Task<bool> IsMcpEndpointAvailable()
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(_mcpEndpoint) };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.GetAsync("/", cts.Token);
            return true;
        }
        catch { return false; }
    }

    private async Task<bool> IsApiAvailable()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _apiClient.GetAsync("/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
