using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using GrocerEase.Api.Errors;
using GrocerEase.Contracts.Errors;
using GrocerEase.Domain.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Testing;

namespace GrocerEase.IntegrationTests;

/// <summary>
/// Needs no database: an empty body is rejected as malformed before any query runs, but the request still counts
/// against the limiter, which sits ahead of the controller.
/// </summary>
public sealed class RateLimitTests : IDisposable
{
    private const int PermitLimit = 3;

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
                ["RateLimiting:Auth:PermitLimit"] = PermitLimit.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:Auth:WindowSeconds"] = "60",
            }));
            builder.ConfigureLogging(logging => logging.AddFakeLogging());
        });

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("/auth/login")]
    [InlineData("/auth/register")]
    [InlineData("/auth/refresh")]
    [InlineData("/auth/logout")]
    public async Task AuthEndpoint_AfterThePermitIsSpent_ReturnsTheRateLimitProblem(string path)
    {
        using var client = _factory.CreateClient();

        for (var attempt = 0; attempt < PermitLimit; attempt++)
        {
            var allowed = await client.PostAsJsonAsync(path, new { });
            Assert.Equal(HttpStatusCode.BadRequest, allowed.StatusCode);
        }

        var limited = await client.PostAsJsonAsync(path, new { });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.StartsWith("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.True(int.Parse(limited.Headers.GetValues("Retry-After").Single(), CultureInfo.InvariantCulture) is > 0 and <= 60);

        var problem = (await limited.Content.ReadFromJsonAsync<ErrorResponse>(System.Text.Json.JsonSerializerOptions.Web))!;
        Assert.Equal(ApiErrorCodes.RateLimited, problem.Code);
        Assert.Equal(429, problem.Status);
        Assert.Equal("Too Many Requests", problem.Title);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
    }

    [Fact]
    public async Task ARejection_IsLoggedAsAWarningWithThePathAndNoBodyOrToken()
    {
        using var client = _factory.CreateClient();
        for (var attempt = 0; attempt < PermitLimit; attempt++)
            await client.PostAsJsonAsync("/auth/login", new { email = "ada@example.com", password = "Passw0rd!" });

        var limited = await client.PostAsJsonAsync("/auth/login", new { email = "ada@example.com", password = "Passw0rd!" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        var record = Assert.Single(_factory.Services.GetFakeLogCollector().GetSnapshot(), r => r.Id.Id == 1009);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal("/auth/login", record.StructuredState!.Single(p => p.Key == "Path").Value);
        var everything = record.Message + string.Join(' ', record.StructuredState!.Select(p => p.Value));
        Assert.DoesNotContain("Passw0rd!", everything);
        Assert.DoesNotContain("ada@example.com", everything);
    }

    [Fact]
    public async Task ARejection_IsCountedInTheRateLimitMetric()
    {
        var rejections = new MetricCollector<long>(
            _factory.Services.GetRequiredService<IMeterFactory>(), "GrocerEase", "grocerease.auth.ratelimit_rejections");
        using var client = _factory.CreateClient();
        for (var attempt = 0; attempt < PermitLimit; attempt++)
            await client.PostAsJsonAsync("/auth/register", new { });

        await client.PostAsJsonAsync("/auth/register", new { });
        await client.PostAsJsonAsync("/auth/register", new { });

        Assert.Equal(2, rejections.GetMeasurementSnapshot().Sum(m => m.Value));
    }

    [Fact]
    public async Task OtherEndpoints_AreNotCounted()
    {
        using var client = _factory.CreateClient();

        for (var attempt = 0; attempt < PermitLimit + 2; attempt++)
        {
            var response = await client.GetAsync("/profile");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
