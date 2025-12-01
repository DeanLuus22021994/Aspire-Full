using System.Net;
using System.Net.Http.Json;

namespace Aspire_Full.Tests.E2E.Api;

/// <summary>
/// End-to-end API tests using HttpClient.
/// Tests the full API stack including database integration.
/// </summary>
[TestFixture]
public class ItemsApiTests
{
    private HttpClient _httpClient = null!;
    private string _baseUrl = null!;

    [SetUp]
    public void Setup()
    {
        // In a full E2E setup, this would use Aspire.Hosting.Testing
        // For now, we use a configurable base URL for testing against running services
        _baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5000";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl)
        };
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    #region GET Tests

    [Test]
    [Category("Integration")]
    public async Task GetItems_ReturnsSuccessStatusCode()
    {
        // Skip if API is not running
        if (!await IsApiAvailable())
        {
            Assert.Ignore("API is not available. Skipping integration test.");
            return;
        }

        // Act
        var response = await _httpClient.GetAsync("/api/items");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [Category("Integration")]
    public async Task GetItems_ReturnsJsonContent()
    {
        // Skip if API is not running
        if (!await IsApiAvailable())
        {
            Assert.Ignore("API is not available. Skipping integration test.");
            return;
        }

        // Act
        var response = await _httpClient.GetAsync("/api/items");

        // Assert
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }

    [Test]
    [Category("Integration")]
    public async Task GetNonExistentItem_ReturnsNotFound()
    {
        // Skip if API is not running
        if (!await IsApiAvailable())
        {
            Assert.Ignore("API is not available. Skipping integration test.");
            return;
        }

        // Act
        var response = await _httpClient.GetAsync("/api/items/99999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region POST Tests

    [Test]
    [Category("Integration")]
    public async Task CreateItem_ReturnsCreatedStatus()
    {
        // Skip if API is not running
        if (!await IsApiAvailable())
        {
            Assert.Ignore("API is not available. Skipping integration test.");
            return;
        }

        // Arrange
        var newItem = new { Name = $"E2E Test Item {Guid.NewGuid()}", Description = "Created by E2E test" };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/items", newItem);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    [Category("Integration")]
    public async Task CreateItem_ReturnsLocationHeader()
    {
        // Skip if API is not running
        if (!await IsApiAvailable())
        {
            Assert.Ignore("API is not available. Skipping integration test.");
            return;
        }

        // Arrange
        var newItem = new { Name = $"E2E Location Test {Guid.NewGuid()}", Description = "Testing location header" };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/items", newItem);

        // Assert
        Assert.That(response.Headers.Location, Is.Not.Null);
        Assert.That(response.Headers.Location?.ToString(), Does.Contain("/api/items/"));
    }

    #endregion

    #region Full CRUD Flow

    [Test]
    [Category("Integration")]
    public async Task FullCrudFlow_CreateReadUpdateDelete()
    {
        // Skip if API is not running
        if (!await IsApiAvailable())
        {
            Assert.Ignore("API is not available. Skipping integration test.");
            return;
        }

        // Create
        var createDto = new { Name = $"CRUD Test {Guid.NewGuid()}", Description = "Testing full CRUD" };
        var createResponse = await _httpClient.PostAsJsonAsync("/api/items", createDto);
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var createdItem = await createResponse.Content.ReadFromJsonAsync<ItemResponse>();
        Assert.That(createdItem, Is.Not.Null);
        Assert.That(createdItem!.Id, Is.GreaterThan(0));

        // Read
        var getResponse = await _httpClient.GetAsync($"/api/items/{createdItem.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var fetchedItem = await getResponse.Content.ReadFromJsonAsync<ItemResponse>();
        Assert.That(fetchedItem?.Name, Is.EqualTo(createDto.Name));

        // Update
        var updateDto = new { Name = "Updated CRUD Test", Description = "Updated description" };
        var updateResponse = await _httpClient.PutAsJsonAsync($"/api/items/{createdItem.Id}", updateDto);
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updatedItem = await updateResponse.Content.ReadFromJsonAsync<ItemResponse>();
        Assert.That(updatedItem?.Name, Is.EqualTo("Updated CRUD Test"));

        // Delete
        var deleteResponse = await _httpClient.DeleteAsync($"/api/items/{createdItem.Id}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify deleted
        var verifyResponse = await _httpClient.GetAsync($"/api/items/{createdItem.Id}");
        Assert.That(verifyResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region Health Check

    [Test]
    [Category("Integration")]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        // Skip if API is not running
        if (!await IsApiAvailable())
        {
            Assert.Ignore("API is not available. Skipping integration test.");
            return;
        }

        // Act
        var response = await _httpClient.GetAsync("/health");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [Category("Integration")]
    public async Task AliveEndpoint_ReturnsHealthy()
    {
        // Skip if API is not running
        if (!await IsApiAvailable())
        {
            Assert.Ignore("API is not available. Skipping integration test.");
            return;
        }

        // Act
        var response = await _httpClient.GetAsync("/alive");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    #endregion

    #region Helper Methods

    private async Task<bool> IsApiAvailable()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync("/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    private record ItemResponse(int Id, string Name, string? Description, DateTime CreatedAt, DateTime UpdatedAt);
}
