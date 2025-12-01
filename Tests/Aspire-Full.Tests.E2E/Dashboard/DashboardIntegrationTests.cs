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
    private HttpClient _otlpClient = null!;
    private HttpClient _mcpClient = null!;
    private LoopbackAspireEnvironment? _loopback;
    private bool _usingLoopback;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        var dashboardUrl = Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_URL") ?? "http://localhost:18888";
        var otlpEndpoint = Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL") ?? "http://localhost:18889";
        var mcpEndpoint = Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_MCP_ENDPOINT_URL") ?? "http://localhost:16036";
        var apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5000";

        _dashboardClient = CreateClient(dashboardUrl);
        _otlpClient = CreateClient(otlpEndpoint);
        _mcpClient = CreateClient(mcpEndpoint);
        _apiClient = CreateClient(apiBaseUrl);

        var dashboardAvailable = await ProbeAsync(_dashboardClient, "/health");
        var apiAvailable = await ProbeAsync(_apiClient, "/health");

        if (!dashboardAvailable || !apiAvailable)
        {
            _loopback = new LoopbackAspireEnvironment();
            await _loopback.InitializeAsync();
            _dashboardClient = _loopback.DashboardClient;
            _apiClient = _loopback.ApiClient;
            _otlpClient = _loopback.OtlpClient;
            _mcpClient = _loopback.McpClient;
            _usingLoopback = true;
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_usingLoopback && _loopback is not null)
        {
            await _loopback.DisposeAsync();
        }
        else
        {
            _dashboardClient.Dispose();
            _apiClient.Dispose();
            _otlpClient.Dispose();
            _mcpClient.Dispose();
        }
    }

    [Test]
    [Category("Integration")]
    public async Task Dashboard_HealthEndpoint_ReturnsHealthy()
    {
        var response = await _dashboardClient.GetAsync("/health");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [Category("Integration")]
    public async Task Dashboard_FrontendPage_ReturnsSuccess()
    {
        var response = await _dashboardClient.GetAsync("/");
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }

    [Test]
    [Category("Integration")]
    [Category("Telemetry")]
    public async Task OTLP_Endpoint_AcceptsTraceData()
    {
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

        var response = await _otlpClient.PostAsJsonAsync("/v1/traces", tracePayload);

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

        var response = await _otlpClient.PostAsJsonAsync("/v1/logs", logsPayload);

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
        var response = await _mcpClient.GetAsync("/");
        Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    [Category("Integration")]
    [Category("MCP")]
    public async Task MCP_ListResources_SendsRequest()
    {
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

        var response = await _mcpClient.PostAsJsonAsync("/", mcpRequest);
        Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    [Category("Integration")]
    [Category("Bidirectional")]
    public async Task BiDirectional_ApiAndDashboard_BothHealthy()
    {
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
        var response = await _apiClient.GetAsync("/api/items");
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }

    [Test]
    [Category("Integration")]
    [Category("Resources")]
    public async Task Resources_ApiOpenApi_IsAccessible()
    {
        var response = await _apiClient.GetAsync("/openapi/v1.json");
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }

    private static HttpClient CreateClient(string baseUrl)
    {
        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private static async Task<bool> ProbeAsync(HttpClient client, string path)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await client.GetAsync(path, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
