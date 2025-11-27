/// <summary>
/// Aspire Full AppHost - Distributed Application Orchestrator
/// </summary>
/// <remarks>
/// This is the main entry point for the .NET Aspire application.
/// It configures and orchestrates all services in the distributed application.
///
/// <para>
/// <strong>Features:</strong>
/// <list type="bullet">
///   <item>Service discovery and configuration</item>
///   <item>Container orchestration</item>
///   <item>OpenTelemetry integration</item>
///   <item>Health checks and monitoring</item>
/// </list>
/// </para>
///
/// <para>
/// <strong>Environment Variables:</strong>
/// <list type="bullet">
///   <item><c>DOTNET_DASHBOARD_OTLP_ENDPOINT_URL</c> - OTLP endpoint for telemetry</item>
///   <item><c>ASPIRE_ALLOW_UNSECURED_TRANSPORT</c> - Allow HTTP for development</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Run the AppHost
/// dotnet run --project Aspire-Full
///
/// // Or use Aspire CLI
/// aspire run
/// </code>
/// </example>

var builder = DistributedApplication.CreateBuilder(args);

// Build and run the distributed application
builder.Build().Run();
