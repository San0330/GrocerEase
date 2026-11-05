using System.Reflection;
using GrocerEase.Application.Common.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GrocerEase.Api.Telemetry;

/// <summary>
/// Traces, metrics and logs in the OpenTelemetry format. The instrumentation is always on, because it costs little and
/// tests read it, but nothing leaves the process unless an OTLP endpoint is configured. The database's own spans and
/// metrics are registered by Infrastructure, so this class, like the rest of the Api, never names a database type.
/// </summary>
public static class TelemetryExtensions
{
    public const string ServiceName = "grocerease-api";

    /// <summary>
    /// The standard OpenTelemetry variable. Setting it (for example <c>http://localhost:4317</c>) turns the export on;
    /// leaving it unset keeps tests and plain <c>dotnet run</c> free of connection errors from a collector that is not there.
    /// </summary>
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public static WebApplicationBuilder AddGrocerEaseTelemetry(this WebApplicationBuilder builder)
    {
        var exporting = !string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]);

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName, serviceVersion: Version()))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    // An orchestrator polls these every few seconds. Tracing them would bury the real requests.
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health")))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(GrocerEaseMetrics.MeterName));

        if (exporting)
        {
            // Traces, metrics and logs all go to the one endpoint, which the variable above names.
            telemetry.UseOtlpExporter();

            // The same log lines the console shows, with the trace and span ids attached, so a log can be opened from
            // its trace. Scopes carry the request's ids; the formatted message is kept for the dashboard's text view.
            builder.Logging.AddOpenTelemetry(logs =>
            {
                logs.IncludeFormattedMessage = true;
                logs.IncludeScopes = true;
            });
        }

        return builder;
    }

    private static string Version() =>
        typeof(TelemetryExtensions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}
