using System;
using System.IO;
using Aspire_Full.ServiceDefaults.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private const string DefaultOtlpEndpoint = "http://localhost:4318";
    private const string SerilogConfigFile = "logging.serilog.yaml";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureSerilogPipeline();
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    private static void ConfigureSerilogPipeline<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var configBasePath = builder.Environment.ContentRootPath;
        var primaryPath = Path.Combine(configBasePath, SerilogConfigFile);
        var hasConfig = File.Exists(primaryPath);

        if (!hasConfig)
        {
            var fallbackPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", SerilogConfigFile));
            if (File.Exists(fallbackPath))
            {
                configBasePath = Path.GetDirectoryName(fallbackPath)!;
                primaryPath = fallbackPath;
                hasConfig = true;
            }
        }

        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(configBasePath)
            .AddYamlFile(Path.GetFileName(primaryPath), optional: !hasConfig, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "SERILOG_");

        var configuration = configurationBuilder.Build();

        Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "logs"));

        var loggerConfiguration = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName);

        if (!hasConfig)
        {
            loggerConfiguration
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.Async(writeTo => writeTo.File(Path.Combine(builder.Environment.ContentRootPath, "logs", "aspire-fallback-.log"), rollingInterval: RollingInterval.Day));
        }

        Log.Logger = loggerConfiguration.CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(dispose: true);
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var configuredEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var headers = builder.Configuration["OTEL_EXPORTER_OTLP_HEADERS"];
        var protocolSetting = builder.Configuration["OTEL_EXPORTER_OTLP_PROTOCOL"];

        if (string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", DefaultOtlpEndpoint);
        }

        if (!string.IsNullOrWhiteSpace(headers))
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", headers);
        }

        if (!string.IsNullOrWhiteSpace(protocolSetting))
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", protocolSetting);
        }
        else
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
        }

        builder.Services.AddOpenTelemetry().UseOtlpExporter();

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
            // Add GPU health check for tensor compute services
            .AddCheck<GpuHealthCheck>("gpu", HealthStatus.Degraded, ["gpu", "compute"]);

        // Register GPU health check options from configuration
        builder.Services.Configure<GpuHealthCheckOptions>(options =>
        {
            var section = builder.Configuration.GetSection("HealthChecks:Gpu");
            if (section.Exists())
            {
                options.RequireGpu = section.GetValue("RequireGpu", false);
                options.WarningVramThresholdPercent = section.GetValue("WarningVramThresholdPercent", 80.0);
                options.CriticalVramThresholdPercent = section.GetValue("CriticalVramThresholdPercent", 95.0);
                options.MinimumVramMb = section.GetValue("MinimumVramMb", 0L);
            }
        });

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });

            // GPU-specific health endpoint for monitoring tensor compute resources
            app.MapHealthChecks("/health/gpu", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("gpu")
            });
        }

        return app;
    }
}
