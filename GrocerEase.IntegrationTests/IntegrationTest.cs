using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GrocerEase.Contracts.Authentication;

namespace GrocerEase.IntegrationTests;

public abstract class IntegrationTest : IDisposable
{
    protected static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    protected IntegrationTest(GrocerEaseApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected GrocerEaseApiFactory Factory { get; }

    protected HttpClient Client { get; }

    public void Dispose() => Client.Dispose();

    protected static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    protected async Task<AuthenticationResponse> RegisterAsync(
        string? email = null,
        string password = "Passw0rd!")
    {
        email ??= UniqueEmail();
        var response = await Client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("Ada", "Lovelace", email, password));

        return await ReadAsync<AuthenticationResponse>(response, HttpStatusCode.OK);
    }

    protected async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, body);
        var value = JsonSerializer.Deserialize<T>(body, Json);
        Assert.NotNull(value);
        return value;
    }

    protected static void UseBearer(HttpRequestMessage request, string token) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
