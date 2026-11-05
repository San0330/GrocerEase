using System.Net;
using System.Net.Http.Json;
using GrocerEase.Api.Errors;
using GrocerEase.Contracts.Authentication;
using GrocerEase.Contracts.Errors;
using GrocerEase.Domain.Exceptions;
using GrocerEase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrocerEase.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class RefreshTokenEndpointsTests(GrocerEaseApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Refresh_ReturnsANewPair_AndTheNewAccessTokenWorks()
    {
        var registered = await RegisterAsync();

        var refreshed = await RefreshAsync(registered.RefreshToken, HttpStatusCode.OK);

        Assert.Equal(registered.Id, refreshed.Id);
        Assert.NotEqual(registered.RefreshToken, refreshed.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.Token));
        using var profile = new HttpRequestMessage(HttpMethod.Get, "/profile");
        UseBearer(profile, refreshed.Token);
        Assert.Equal(HttpStatusCode.OK, (await Client.SendAsync(profile)).StatusCode);
    }

    [Fact]
    public async Task Refresh_WithTheNewToken_KeepsWorkingDownTheChain()
    {
        var registered = await RegisterAsync();

        var second = await ReadAsync<AuthenticationResponse>(await PostRefreshAsync(registered.RefreshToken), HttpStatusCode.OK);
        var third = await ReadAsync<AuthenticationResponse>(await PostRefreshAsync(second.RefreshToken), HttpStatusCode.OK);

        Assert.NotEqual(second.RefreshToken, third.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithAnUnknownToken_ReturnsTheInvalidRefreshTokenProblem()
    {
        var response = await PostRefreshAsync("no-such-token");

        await AssertInvalidRefreshTokenAsync(response);
    }

    [Fact]
    public async Task Refresh_WithAUsedToken_IsRejected_AndRevokesTheWholeFamily()
    {
        var registered = await RegisterAsync();
        var rotated = await RefreshAsync(registered.RefreshToken, HttpStatusCode.OK);

        // The first token has been replaced, so presenting it again means someone kept a copy.
        await AssertInvalidRefreshTokenAsync(await PostRefreshAsync(registered.RefreshToken));

        // The replacement is revoked with it: the thief and the owner both have to log in again.
        await AssertInvalidRefreshTokenAsync(await PostRefreshAsync(rotated.RefreshToken));

        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var family = await database.RefreshTokens.Where(t => t.UserId == registered.Id).ToListAsync();
        Assert.Equal(2, family.Count);
        Assert.All(family, t => Assert.True(t.IsRevoked));
    }

    [Fact]
    public async Task Refresh_AfterAReuse_LeavesTheOtherSessionsAlone()
    {
        var email = UniqueEmail();
        var registered = await RegisterAsync(email);
        var otherSession = await ReadAsync<AuthenticationResponse>(
            await Client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "Passw0rd!")),
            HttpStatusCode.OK);
        await RefreshAsync(registered.RefreshToken, HttpStatusCode.OK);

        await AssertInvalidRefreshTokenAsync(await PostRefreshAsync(registered.RefreshToken));

        await RefreshAsync(otherSession.RefreshToken, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithAnExpiredToken_IsRejected()
    {
        var registered = await RegisterAsync();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await database.RefreshTokens
                .Where(t => t.UserId == registered.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        await AssertInvalidRefreshTokenAsync(await PostRefreshAsync(registered.RefreshToken));
    }

    [Fact]
    public async Task Refresh_WithAMissingBody_IsMalformed()
    {
        var response = await Client.PostAsJsonAsync("/auth/refresh", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithABlankToken_IsAValidationFailure()
    {
        var response = await PostRefreshAsync("");

        var problem = await ReadAsync<ValidationErrorResponse>(response, HttpStatusCode.UnprocessableEntity);
        Assert.True(problem.Errors.ContainsKey("refreshToken"));
    }

    private async Task<AuthenticationResponse> RefreshAsync(string refreshToken, HttpStatusCode expected) =>
        await ReadAsync<AuthenticationResponse>(await PostRefreshAsync(refreshToken), expected);

    private Task<HttpResponseMessage> PostRefreshAsync(string refreshToken) =>
        Client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(refreshToken));

    private async Task AssertInvalidRefreshTokenAsync(HttpResponseMessage response)
    {
        var problem = await ReadAsync<ErrorResponse>(response, HttpStatusCode.Unauthorized);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, problem.Code);
        Assert.Equal(401, problem.Status);
        Assert.Equal("The refresh token is invalid or has expired.", problem.Detail);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
