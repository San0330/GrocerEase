using GrocerEase.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace GrocerEase.IntegrationTests;

public class GrocerEaseApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSecret = "integration-test-secret-key-32b-min";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .Build();

    private string? _connectionString;

    /// <summary>When true the host migrates the empty database itself while it starts, as it does under compose.</summary>
    protected virtual bool MigrateOnStartup => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Every log record is kept, so tests can assert what was written and, above all, what was not.
        builder.ConfigureLogging(logging => logging.AddFakeLogging());
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["JwtSettings:Secret"] = JwtSecret,
                ["JwtSettings:Issuer"] = "GrocerEase",
                ["JwtSettings:Audience"] = "GrocerEase",
                ["JwtSettings:ExpiryMinutes"] = "60",
                ["PasswordHashing:Cost"] = "11",
                // High enough that no other test is throttled; RateLimitTests uses its own host with a low limit.
                ["RateLimiting:Auth:PermitLimit"] = "100000",
                ["Database:MigrateOnStartup"] = MigrateOnStartup.ToString(),
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        // Building the host is what runs the startup migration, so there is nothing more to do in that mode.
        using var scope = Services.CreateScope();
        if (!MigrateOnStartup)
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await database.Database.MigrateAsync();
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
