using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using GrocerEase.Contracts.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace GrocerEase.IntegrationTests;

/// <summary>Whether and what the host exports. The export is off unless an OTLP endpoint is configured.</summary>
public sealed class TelemetryTests
{
    private const string Endpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";

    [Fact]
    public async Task WithNoEndpoint_TheHostServesRequests_AndDoesNotRegisterTheLogExporter()
    {
        using var factory = NoDatabaseHost(endpoint: null);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(factory.Services.GetServices<ILoggerProvider>(), IsOpenTelemetryLogProvider);
    }

    [Fact]
    public async Task WithAnEndpoint_TheLogExporterIsRegistered()
    {
        using var factory = NoDatabaseHost(endpoint: "http://127.0.0.1:1");
        using var client = factory.CreateClient();

        await client.GetAsync("/health/live");

        Assert.Contains(factory.Services.GetServices<ILoggerProvider>(), IsOpenTelemetryLogProvider);
    }

    [Fact]
    public async Task WithACollectorThatCannotBeReached_RequestsStillSucceed()
    {
        // Port 1 refuses connections. Telemetry is a side channel: its failure must never become the caller's failure.
        using var factory = NoDatabaseHost(endpoint: "http://127.0.0.1:1");
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await client.PostAsJsonAsync("/auth/login", new { });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task AHealthCheck_IsNotTraced_SoOrchestratorPollingDoesNotFillTheTraceStore()
    {
        var spans = new List<Activity>();
        using var factory = NoDatabaseHost(endpoint: null, spans);
        using var client = factory.CreateClient();

        await client.GetAsync("/health/live");
        await client.PostAsJsonAsync("/auth/login", new { });
        // The health request finished first, so once the login span is in, a health span would be too.
        await WaitForAsync(factory, spans, span => span.DisplayName.Contains("auth/login", StringComparison.Ordinal));

        var names = string.Join(" | ", Snapshot(spans).Select(span => $"{span.Kind}:{span.DisplayName}"));
        Assert.True(Snapshot(spans).Any(span => span.DisplayName.Contains("auth/login", StringComparison.Ordinal)), "spans: " + names);
        Assert.DoesNotContain(Snapshot(spans), span => span.DisplayName.Contains("health", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EverySpanCarriesTheServiceName_SoItsTelemetryCanBeFound()
    {
        var spans = new List<Activity>();
        using var factory = NoDatabaseHost(endpoint: null, spans);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/auth/login", new { });
        await WaitForAsync(factory, spans, span => span.Kind == ActivityKind.Server);

        Assert.Contains(Snapshot(spans), span => span.Kind == ActivityKind.Server);
        Assert.Equal("grocerease-api", ResourceOf(factory, "service.name"));
    }

    private static bool IsOpenTelemetryLogProvider(ILoggerProvider provider) =>
        provider.GetType().FullName == "OpenTelemetry.Logs.OpenTelemetryLoggerProvider";

    // The server span is stopped just after the test client has its response, so a test that reads the list straight
    // away can see it empty. Wait for the span it needs, up to a limit.
    private static async Task WaitForAsync(WebApplicationFactory<Program> factory, List<Activity> spans, Func<Activity, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            factory.Services.GetRequiredService<TracerProvider>().ForceFlush();
            if (Snapshot(spans).Any(predicate))
                return;

            await Task.Delay(25);
        }
    }

    // The exporter adds to the list from the thread that stopped the span, so copy it before reading.
    private static Activity[] Snapshot(List<Activity> spans)
    {
        while (true)
        {
            try
            {
                return spans.ToArray();
            }
            catch (InvalidOperationException)
            {
                // Added to while copying. Try again.
            }
        }
    }

    private static string? ResourceOf(WebApplicationFactory<Program> factory, string key) =>
        factory.Services.GetRequiredService<TracerProvider>().GetResource()
            .Attributes.SingleOrDefault(attribute => attribute.Key == key).Value?.ToString();

    private static WebApplicationFactory<Program> NoDatabaseHost(string? endpoint, List<Activity>? spans = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            // Program.cs decides whether to export while it is still building the host, which is before the configuration
            // added below is merged in. UseSetting is visible that early.
            if (endpoint is not null)
                builder.UseSetting(Endpoint, endpoint);

            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
                    ["JwtSettings:Secret"] = GrocerEaseApiFactory.JwtSecret,
                    ["JwtSettings:Issuer"] = "GrocerEase",
                    ["JwtSettings:Audience"] = "GrocerEase",
                    ["JwtSettings:ExpiryMinutes"] = "60",
                    ["RateLimiting:Auth:PermitLimit"] = "100000",
                };
                config.AddInMemoryCollection(settings);
            });

            if (spans is not null)
                builder.ConfigureServices(services =>
                    services.ConfigureOpenTelemetryTracerProvider((_, tracing) => tracing.AddInMemoryExporter(spans)));
        });
}
