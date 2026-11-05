using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace GrocerEase.IntegrationTests;

/// <summary>Needs no database: an empty body is rejected before any query, and an unknown route never reaches one.</summary>
public sealed class RequestLoggingTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
                ["JwtSettings:Secret"] = GrocerEaseApiFactory.JwtSecret,
                ["JwtSettings:Issuer"] = "GrocerEase",
                ["JwtSettings:Audience"] = "GrocerEase",
                ["JwtSettings:ExpiryMinutes"] = "60",
                ["RateLimiting:Auth:PermitLimit"] = "100000",
            }));
            builder.ConfigureLogging(logging => logging.AddFakeLogging());
        });

    public void Dispose() => _factory.Dispose();

    private FakeLogCollector Logs => _factory.Services.GetFakeLogCollector();

    [Fact]
    public async Task ARequest_ProducesOneLineWithTheRouteAndTheStatusTheClientGot()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var line = Assert.Single(RequestLines());
        Assert.StartsWith("POST /auth/login responded 400", line.Message);
        Assert.Equal(LogLevel.Information, line.Level);
    }

    [Fact]
    public async Task AnUnknownRoute_IsLoggedAsUnmatched_NotByItsPath()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/wp-login.php?probe=1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var line = Assert.Single(RequestLines());
        Assert.StartsWith("GET unmatched responded 404", line.Message);
        Assert.DoesNotContain("wp-login", line.Message);
    }

    [Fact]
    public async Task ASuccessfulLivenessCheck_IsNotLoggedAtInformation()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(RequestLines());
    }

    [Fact]
    public async Task AUnauthorizedRequest_IsLoggedWithoutTheToken()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/profile");
        request.Headers.Authorization = new("Bearer", "eyJ.not-a-real-token.value");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var line = Assert.Single(RequestLines());
        Assert.StartsWith("GET /profile responded 401", line.Message);
        Assert.DoesNotContain("eyJ", line.Message);
    }

    [Fact]
    public void TheHost_TracksTheTraceIdOnEveryLogLine()
    {
        var tracking = _factory.Services.GetRequiredService<IOptions<LoggerFactoryOptions>>().Value.ActivityTrackingOptions;

        Assert.True(tracking.HasFlag(ActivityTrackingOptions.TraceId));
        Assert.True(tracking.HasFlag(ActivityTrackingOptions.SpanId));
    }

    [Fact]
    public void ByDefaultTheConsoleWritesJson_SoALogShipperCanParseIt()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("json", configuration["Logging:Console:FormatterName"]);
        Assert.Equal("True", configuration["Logging:Console:FormatterOptions:IncludeScopes"]);
    }

    private IEnumerable<FakeLogRecord> RequestLines() => Logs.GetSnapshot()
        .Where(record => record.Category == "GrocerEase.Api.MiddleWare.RequestLoggingMiddleware"
                         && record.Level >= LogLevel.Information);
}
