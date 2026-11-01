using System.Net;
using System.Net.Http.Json;
using GrocerEase.Contracts.Authentication;

namespace GrocerEase.IntegrationTests;

public sealed class StartupMigrationTests : IClassFixture<StartupMigrationTests.MigratingApiFactory>
{
    private readonly MigratingApiFactory _factory;

    public StartupMigrationTests(MigratingApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_AgainstAnEmptyDatabase_WorksBecauseTheHostMigratedItOnStartup()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("Ada", "Lovelace", $"{Guid.NewGuid():N}@example.com", "Passw0rd!"));

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    public sealed class MigratingApiFactory : GrocerEaseApiFactory
    {
        protected override bool MigrateOnStartup => true;
    }
}
