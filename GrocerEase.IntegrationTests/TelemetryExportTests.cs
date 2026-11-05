using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using GrocerEase.Contracts.Authentication;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace GrocerEase.IntegrationTests;

/// <summary>What one request looks like as a trace and as metrics, against a real Postgres.</summary>
public sealed class TelemetryExportTests : IClassFixture<TelemetryExportTests.TracedApiFactory>
{
    private readonly TracedApiFactory _factory;

    public TelemetryExportTests(TracedApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ARegistration_IsOneTrace_WithTheDatabaseCallsAsChildrenOfTheRequest()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("Ada", "Lovelace", $"{Guid.NewGuid():N}@example.com", "Passw0rd!"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var server = await WaitForServerSpanAsync("auth/register");
        var inTrace = _factory.Spans().Where(span => span.TraceId == server.TraceId).ToArray();
        var databaseCalls = inTrace.Where(span => span.Kind == ActivityKind.Client && span.Source.Name == "Npgsql").ToArray();

        Assert.NotEmpty(databaseCalls);
        // Every database call is inside the request, however deep: following the parent links reaches the request span.
        Assert.All(databaseCalls, call => Assert.True(
            DescendsFrom(call, server, inTrace),
            $"'{call.DisplayName}' is not under '{server.DisplayName}'"));
    }

    [Fact]
    public async Task TheSpanName_IsTheRoutePattern_NotThePathWithItsValues()
    {
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/auth/login", new LoginRequest($"{Guid.NewGuid():N}@example.com", "Passw0rd!"));

        var server = await WaitForServerSpanAsync("auth/login");
        Assert.Equal("POST auth/login", server.DisplayName);
        Assert.DoesNotContain(server.TagObjects, tag => tag.Value?.ToString()?.Contains("Passw0rd!") == true);
    }

    [Fact]
    public async Task AFailedLogin_ShowsUpInTheExportedMetrics_AndBCryptIsTimed()
    {
        using var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/auth/register", new RegisterRequest("Ada", "Lovelace", email, "Passw0rd!"));
        await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "wrong-password"));

        _factory.Services.GetRequiredService<MeterProvider>().ForceFlush();
        var metrics = _factory.Metrics();

        Assert.Contains("login/failure", AttemptCounts(metrics).Keys);
        Assert.Contains("register/success", AttemptCounts(metrics).Keys);
        Assert.Contains(metrics, metric => metric.Name == "grocerease.password.hash.duration" && metric.Unit == "s");
        // The framework's own request metrics arrive through the same pipeline.
        Assert.Contains(metrics, metric => metric.Name == "http.server.request.duration");
    }

    // The attempt counter's value for each "operation/outcome" pair seen so far.
    private static Dictionary<string, long> AttemptCounts(Metric[] metrics)
    {
        var counts = new Dictionary<string, long>();
        foreach (var metric in metrics.Where(m => m.Name == "grocerease.auth.attempts"))
        {
            foreach (ref readonly var point in metric.GetMetricPoints())
            {
                string? operation = null, outcome = null;
                foreach (var tag in point.Tags)
                {
                    if (tag.Key == "operation") operation = tag.Value?.ToString();
                    if (tag.Key == "outcome") outcome = tag.Value?.ToString();
                }

                counts[$"{operation}/{outcome}"] = point.GetSumLong();
            }
        }

        return counts;
    }

    private async Task<Activity> WaitForServerSpanAsync(string route)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            _factory.Services.GetRequiredService<TracerProvider>().ForceFlush();
            var found = _factory.Spans().FirstOrDefault(span =>
                span.Kind == ActivityKind.Server && span.DisplayName.EndsWith(route, StringComparison.Ordinal));
            if (found is not null)
                return found;

            await Task.Delay(25);
        }

        throw new Xunit.Sdk.XunitException(
            $"No server span for '{route}'. Seen: " + string.Join(" | ", _factory.Spans().Select(s => s.DisplayName)));
    }

    private static bool DescendsFrom(Activity span, Activity ancestor, Activity[] trace)
    {
        for (var parent = span.ParentSpanId; parent != default;)
        {
            if (parent == ancestor.SpanId)
                return true;

            parent = trace.FirstOrDefault(candidate => candidate.SpanId == parent)?.ParentSpanId ?? default;
        }

        return false;
    }

    public sealed class TracedApiFactory : GrocerEaseApiFactory
    {
        private readonly List<Activity> _spans = [];
        private readonly List<Metric> _metrics = [];

        public Metric[] Metrics() => _metrics.ToArray();

        public Activity[] Spans()
        {
            while (true)
            {
                try
                {
                    return _spans.ToArray();
                }
                catch (InvalidOperationException)
                {
                    // Added to while copying. Try again.
                }
            }
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.ConfigureOpenTelemetryTracerProvider((_, tracing) => tracing.AddInMemoryExporter(_spans));
                services.ConfigureOpenTelemetryMeterProvider((_, metrics) => metrics.AddInMemoryExporter(_metrics));
            });
        }
    }
}
