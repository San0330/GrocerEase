using System.Net;
using System.Net.Http.Json;
using GrocerEase.Contracts.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace GrocerEase.IntegrationTests;

/// <summary>
/// The whole authentication story through the real host, then a read of everything it logged. The point is the second
/// half: no password, token or address may appear anywhere in the log, however the events are worded.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuditLoggingTests(GrocerEaseApiFactory factory) : IntegrationTest(factory)
{
    private const string Password = "Passw0rd!";

    [Fact]
    public async Task RegisterLoginRefreshReuseAndLogout_EachLeaveTheirEvent()
    {
        var email = UniqueEmail();
        var registered = await RegisterAsync(email);
        var loggedIn = await LoginAsync(email, Password);
        var refreshed = await RefreshAsync(registered.RefreshToken);
        await Client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(registered.RefreshToken)); // reuse
        await Client.PostAsJsonAsync("/auth/logout", new LogoutRequest(loggedIn.RefreshToken));

        var events = EventsFor(registered.Id);
        Assert.Contains(1001, events);   // registered
        Assert.Contains(1005, events);   // refreshed
        Assert.Contains(1007, events);   // reuse detected
        Assert.Contains(1003, events);   // logged in
        Assert.Contains(1008, events);   // logged out
        Assert.Equal(registered.Id, loggedIn.Id);
        Assert.NotEqual(registered.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task AFailedLoginAndADuplicateRegistration_AreLoggedWithoutTheEmailAddress()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);
        await Client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "wrong-password"));
        await Client.PostAsJsonAsync("/auth/register", new RegisterRequest("Ada", "Lovelace", email, Password));

        var records = Logs().Where(r => r.Id.Id is 1002 or 1004).ToList();

        Assert.Contains(records, r => r.Id.Id == 1004 && r.Level == LogLevel.Warning);
        Assert.Contains(records, r => r.Id.Id == 1002);
        Assert.DoesNotContain(records, r => TextOf(r).Contains(email, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NothingThatWasLogged_ContainsAPasswordATokenOrAnEmailAddress()
    {
        var email = UniqueEmail();
        var registered = await RegisterAsync(email);
        var loggedIn = await LoginAsync(email, Password);
        var refreshed = await RefreshAsync(loggedIn.RefreshToken);
        await Client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "wrong-password"));
        await Client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(loggedIn.RefreshToken)); // reuse
        await Client.PostAsJsonAsync("/auth/logout", new LogoutRequest(registered.RefreshToken));
        using var profile = new HttpRequestMessage(HttpMethod.Get, "/profile");
        UseBearer(profile, refreshed.Token);
        await Client.SendAsync(profile);

        var secrets = new[]
        {
            Password, "wrong-password", email,
            registered.Token, registered.RefreshToken,
            loggedIn.Token, loggedIn.RefreshToken,
            refreshed.Token, refreshed.RefreshToken,
        };
        // A scan that finds nothing proves nothing if the log is empty, so first check that this user's events are there.
        Assert.Contains(Logs(), record => TextOf(record).Contains(registered.Id.ToString()));

        var leaks = Logs()
            .SelectMany(record => secrets
                .Where(secret => TextOf(record).Contains(secret, StringComparison.OrdinalIgnoreCase))
                .Select(secret => $"{record.Category} #{record.Id.Id}: {record.Message}"))
            .ToList();

        Assert.True(leaks.Count == 0, "The log contains a secret: " + string.Join(" | ", leaks));
    }

    private async Task<AuthenticationResponse> LoginAsync(string email, string password) =>
        await ReadAsync<AuthenticationResponse>(
            await Client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password)), HttpStatusCode.OK);

    private async Task<AuthenticationResponse> RefreshAsync(string refreshToken) =>
        await ReadAsync<AuthenticationResponse>(
            await Client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(refreshToken)), HttpStatusCode.OK);

    private IReadOnlyList<FakeLogRecord> Logs() => Factory.Services.GetFakeLogCollector().GetSnapshot();

    private List<int> EventsFor(Guid userId) => Logs()
        .Where(r => r.StructuredState!.Any(p => p.Key == "UserId" && p.Value == userId.ToString()))
        .Select(r => r.Id.Id)
        .ToList();

    // Everything a record can carry: the rendered text, every structured value, the scopes, and the exception.
    private static string TextOf(FakeLogRecord record) =>
        record.Message
        + " " + string.Join(' ', record.StructuredState!.Select(p => p.Value))
        + " " + string.Join(' ', record.Scopes.Select(s => s?.ToString()))
        + " " + record.Exception;
}
