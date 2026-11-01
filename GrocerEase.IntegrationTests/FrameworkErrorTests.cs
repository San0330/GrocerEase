using System.Net;
using System.Text;
using GrocerEase.Api.Errors;
using GrocerEase.Contracts.Errors;

namespace GrocerEase.IntegrationTests;

/// <summary>
/// Responses the framework would end with an empty body or its own ProblemDetails (unknown route, wrong method,
/// wrong content type) must come back in the same error shape as everything else.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class FrameworkErrorTests(GrocerEaseApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task UnknownRoute_Returns404InTheApiShape()
    {
        var response = await Client.GetAsync("/nothing/here");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, ApiErrorCodes.NotFound);
    }

    [Fact]
    public async Task WrongMethod_Returns405InTheApiShape_AndKeepsTheAllowHeader()
    {
        var response = await Client.DeleteAsync("/auth/login");

        await AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed, ApiErrorCodes.MethodNotAllowed);
        Assert.Contains("POST", response.Content.Headers.Allow);
    }

    [Fact]
    public async Task NonJsonContentType_Returns415InTheApiShape()
    {
        using var content = new StringContent("email=ada@example.com", Encoding.UTF8, "text/plain");

        var response = await Client.PostAsync("/auth/login", content);

        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, ApiErrorCodes.UnsupportedMediaType);
    }

    private async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ReadAsync<ErrorResponse>(response, status);
        Assert.Equal(code, problem.Code);
        Assert.Equal((int)status, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }
}
