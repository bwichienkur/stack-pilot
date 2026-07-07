using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackPilot.Application.Common;

namespace StackPilot.Infrastructure.Extensions;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddStackPilotOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string defaultServiceName,
        bool includeAspNetCoreInstrumentation = true)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? defaultServiceName;
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                if (includeAspNetCoreInstrumentation)
                    tracing.AddAspNetCoreInstrumentation();
                tracing
                    .AddHttpClientInstrumentation()
                    .AddSource(StackPilotTelemetry.SourceName);

                if (!string.IsNullOrEmpty(otlpEndpoint))
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                else
                    tracing.AddConsoleExporter();
            })
            .WithMetrics(metrics =>
            {
                if (includeAspNetCoreInstrumentation)
                    metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                else
                    metrics.AddConsoleExporter();
            });

        return services;
    }
}
