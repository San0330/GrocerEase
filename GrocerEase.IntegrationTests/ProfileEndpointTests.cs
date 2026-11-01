using System.Net;
using System.Net.Http.Json;
using GrocerEase.Contracts.Errors;
using GrocerEase.Contracts.Profile;
using GrocerEase.Domain.Exceptions;

namespace GrocerEase.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class ProfileEndpointTests(GrocerEaseApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Get_WithoutAToken_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/profile");

        await AssertUnauthorizedProblemAsync(response, expectedChallenge: "Bearer");
    }

    [Fact]
    public async Task Get_WithTheRegistrationToken_ReturnsTheProfile()
    {
        var email = UniqueEmail();
        var registered = await RegisterAsync(email);

        var request = new HttpRequestMessage(HttpMethod.Get, "/profile");
        UseBearer(request, registered.Token);

        var response = await Client.SendAsync(request);
        var profile = await ReadAsync<ProfileResponse>(response, HttpStatusCode.OK);

        Assert.Equal(registered.Id, profile.Id);
        Assert.Equal(email, profile.Email);
    }

    [Fact]
    public async Task Get_WithATamperedToken_ReturnsUnauthorized()
    {
        var registered = await RegisterAsync();
        var tampered = registered.Token[..^1] + (registered.Token[^1] == 'a' ? 'b' : 'a');

        var request = new HttpRequestMessage(HttpMethod.Get, "/profile");
        UseBearer(request, tampered);

        var response = await Client.SendAsync(request);

        await AssertUnauthorizedProblemAsync(response, expectedChallenge: "Bearer error=\"invalid_token\"");
    }

    private async Task AssertUnauthorizedProblemAsync(HttpResponseMessage response, string expectedChallenge)
    {
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedChallenge, response.Headers.WwwAuthenticate.ToString());

        var problem = await ReadAsync<ErrorResponse>(response, HttpStatusCode.Unauthorized);
        Assert.Equal(ErrorCodes.Unauthorized, problem.Code);
        Assert.Equal(401, problem.Status);
        Assert.Equal("Unauthorized", problem.Title);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
    }
}
