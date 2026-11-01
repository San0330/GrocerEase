using System.Net;
using System.Net.Http.Json;
using GrocerEase.Api.Errors;
using GrocerEase.Contracts.Errors;
using GrocerEase.Domain.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
                ["RateLimiting:Auth:PermitLimit"] = PermitLimit.ToString(),
                ["RateLimiting:Auth:WindowSeconds"] = "60",
            }));
        });

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("/auth/login")]
    [InlineData("/auth/register")]
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
        Assert.True(int.Parse(limited.Headers.GetValues("Retry-After").Single()) is > 0 and <= 60);

        var problem = (await limited.Content.ReadFromJsonAsync<ErrorResponse>(System.Text.Json.JsonSerializerOptions.Web))!;
        Assert.Equal(ApiErrorCodes.RateLimited, problem.Code);
        Assert.Equal(429, problem.Status);
        Assert.Equal("Too Many Requests", problem.Title);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
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
