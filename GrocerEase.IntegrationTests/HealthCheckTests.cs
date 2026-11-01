using System.Net;
using GrocerEase.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GrocerEase.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class HealthCheckTests(GrocerEaseApiFactory factory) : IntegrationTest(factory)
{
    [Theory]
    [InlineData(HealthEndpoints.Live)]
    [InlineData(HealthEndpoints.Ready)]
    public async Task WhenTheDatabaseIsUp_BothEndpointsReportHealthy(string path)
    {
        var response = await Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_NeedsNoToken_AndIsNotCountedAgainstTheAuthRateLimit()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await Client.GetAsync(HealthEndpoints.Live);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

/// <summary>An unreachable database must hold traffic back (ready) without making the process look dead (live).</summary>
public sealed class UnreachableDatabaseHealthTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Nothing listens on port 1, so the connection is refused at once.
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=unused;Timeout=2",
                ["JwtSettings:Secret"] = GrocerEaseApiFactory.JwtSecret,
                ["JwtSettings:Issuer"] = "GrocerEase",
                ["JwtSettings:Audience"] = "GrocerEase",
                ["JwtSettings:ExpiryMinutes"] = "60",
            }));
        });

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Ready_ReportsUnhealthyWithoutSayingWhy()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(HealthEndpoints.Ready);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Live_StillReportsHealthy()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(HealthEndpoints.Live);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
