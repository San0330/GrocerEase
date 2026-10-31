using System.Net;
using System.Text;
using GrocerEase.Contracts.Errors;
using GrocerEase.Domain.Exceptions;

namespace GrocerEase.IntegrationTests;

/// <summary>
/// Bodies MVC rejects before the controller runs. They must come back in the API's own error shape,
/// not as the framework's default <c>ValidationProblemDetails</c>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class MalformedRequestTests(GrocerEaseApiFactory factory) : IntegrationTest(factory)
{
    [Theory]
    [InlineData("/auth/login", "{\"email\": ")]
    [InlineData("/auth/login", "not json at all")]
    [InlineData("/auth/login", "")]
    [InlineData("/auth/login", "null")]
    [InlineData("/auth/login", "{}")]
    [InlineData("/auth/login", "{\"email\":\"ada@example.com\"}")]
    [InlineData("/auth/login", "{\"email\":123,\"password\":\"Passw0rd!\"}")]
    [InlineData("/auth/register", "{\"firstName\":\"Ada\"")]
    [InlineData("/auth/register", "{}")]
    public async Task UnreadableOrIncompleteBody_ReturnsTheMalformedRequestProblem(string path, string body)
    {
        var response = await PostAsync(path, body);

        var problem = await ReadAsync<ValidationErrorResponse>(response, HttpStatusCode.BadRequest);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ErrorCodes.MalformedRequest, problem.Code);
        Assert.Equal(400, problem.Status);
        Assert.Equal("Malformed Request", problem.Title);
        Assert.NotEmpty(problem.Errors);
        Assert.All(problem.Errors.Values, messages => Assert.NotEmpty(messages));
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task MissingProperty_NamesTheFieldThatIsMissing()
    {
        var response = await PostAsync("/auth/login", "{\"email\":\"ada@example.com\"}");

        var problem = await ReadAsync<ValidationErrorResponse>(response, HttpStatusCode.BadRequest);
        Assert.True(problem.Errors.ContainsKey("Password"), string.Join(", ", problem.Errors.Keys));
    }

    [Fact]
    public async Task BrokenJson_DoesNotEchoParserInternals_OrTheBoundParameterName()
    {
        var response = await PostAsync("/auth/login", "{\"email\": ");

        var problem = await ReadAsync<ValidationErrorResponse>(response, HttpStatusCode.BadRequest);
        var text = string.Join(" ", problem.Errors.SelectMany(error => error.Value));
        Assert.DoesNotContain("LineNumber", text);
        Assert.DoesNotContain("GrocerEase.Contracts", text);
        Assert.DoesNotContain("request", problem.Errors.Keys);
    }

    [Fact]
    public async Task EmptyValues_StillReachTheValidatorAndReturn422()
    {
        var response = await PostAsync("/auth/login", "{\"email\":\"\",\"password\":\"\"}");

        var problem = await ReadAsync<ValidationErrorResponse>(response, HttpStatusCode.UnprocessableEntity);
        Assert.Equal(ErrorCodes.ValidationFailed, problem.Code);
    }

    [Fact]
    public async Task NonJsonContentType_Returns415()
    {
        using var content = new StringContent("email=ada@example.com", Encoding.UTF8, "text/plain");

        var response = await Client.PostAsync("/auth/login", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    private Task<HttpResponseMessage> PostAsync(string path, string json)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return Client.PostAsync(path, content);
    }
}
