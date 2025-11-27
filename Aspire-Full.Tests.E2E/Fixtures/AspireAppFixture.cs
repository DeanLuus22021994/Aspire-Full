using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire_Full.Tests.E2E.Fixtures;

/// <summary>
/// Fixture that provides a running Aspire distributed application for integration tests.
/// This starts the AppHost with all services and provides access to the dashboard.
/// </summary>
public class AspireAppFixture : IAsyncDisposable
{
    private DistributedApplication? _app;
    private bool _isInitialized;

    /// <summary>
    /// The running distributed application instance.
    /// </summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException("App not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Whether the application has been successfully initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Initializes and starts the Aspire distributed application.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        try
        {
            // Create the app host with test configuration
            var appHost = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Aspire_Full>();

            _app = await appHost.BuildAsync();

            // Start the application and wait for resources
            await _app.StartAsync();

            // Wait for the API to be ready
            await WaitForResourceAsync("api", TimeSpan.FromMinutes(2));

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Aspire app: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Waits for a resource to reach running state.
    /// </summary>
    private async Task WaitForResourceAsync(string resourceName, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        // Simple polling approach - wait for resource endpoint to be available
        var client = GetHttpClient(resourceName);
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var response = await client.GetAsync("/health", cts.Token);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // Resource not ready yet
            }
            await Task.Delay(500, cts.Token);
        }

        throw new TimeoutException($"Resource '{resourceName}' did not become healthy within {timeout}");
    }

    /// <summary>
    /// Gets an HTTP client configured to communicate with the specified resource.
    /// </summary>
    public HttpClient GetHttpClient(string resourceName)
    {
        return App.CreateHttpClient(resourceName);
    }

    /// <summary>
    /// Gets the connection string for a resource.
    /// </summary>
    public async Task<string?> GetConnectionStringAsync(string resourceName)
    {
        return await App.GetConnectionStringAsync(resourceName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
        _isInitialized = false;
        GC.SuppressFinalize(this);
    }
}
