using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Aspire_Full.WebAssembly.Extensions;

public static class ServiceDefaultsExtensions
{
    public static WebAssemblyHostBuilder AddServiceDefaults(this WebAssemblyHostBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddHttpClientInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddHttpClientInstrumentation();
            });

        builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }
}
