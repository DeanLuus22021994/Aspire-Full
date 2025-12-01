using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class WebAssemblyExtensions
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
