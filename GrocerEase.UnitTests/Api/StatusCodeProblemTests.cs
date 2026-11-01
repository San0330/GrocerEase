using System.Text.Json;
using GrocerEase.Api.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GrocerEase.UnitTests.Api;

public class StatusCodeProblemTests
{
    private static readonly IServiceProvider Services = new ServiceCollection().AddLogging().BuildServiceProvider();

    [Theory]
    [InlineData(401, ApiErrorCodes.Unauthorized, "Unauthorized")]
    [InlineData(403, ApiErrorCodes.Forbidden, "Forbidden")]
    [InlineData(404, ApiErrorCodes.NotFound, "Not Found")]
    [InlineData(405, ApiErrorCodes.MethodNotAllowed, "Method Not Allowed")]
    [InlineData(406, ApiErrorCodes.NotAcceptable, "Not Acceptable")]
    [InlineData(415, ApiErrorCodes.UnsupportedMediaType, "Unsupported Media Type")]
    [InlineData(400, ApiErrorCodes.Rejected, "Bad Request")]
    [InlineData(418, ApiErrorCodes.Rejected, "Error")]
    [InlineData(500, ApiErrorCodes.Unexpected, "Internal Server Error")]
    [InlineData(503, ApiErrorCodes.Unexpected, "Error")]
    public async Task WriteAsync_AnswersABareStatusInTheApiErrorShape(int status, string code, string title)
    {
        var context = ContextWithStatus(status);

        await StatusCodeProblem.WriteAsync(new StatusCodeContext(context, new StatusCodePagesOptions(), null!));

        Assert.Equal(status, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);

        var body = await ReadBodyAsync(context);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.Equal(title, body.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("detail").GetString()));
        Assert.Equal("http-trace", body.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task WriteAsync_WhenTheClientHasGone_WritesNothing()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var context = ContextWithStatus(499);
        context.RequestAborted = aborted.Token;

        await StatusCodeProblem.WriteAsync(new StatusCodeContext(context, new StatusCodePagesOptions(), null!));

        Assert.Equal(0, context.Response.Body.Length);
    }

    private static DefaultHttpContext ContextWithStatus(int status)
    {
        var context = new DefaultHttpContext { RequestServices = Services, TraceIdentifier = "http-trace" };
        context.Response.StatusCode = status;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return (await JsonDocument.ParseAsync(context.Response.Body)).RootElement;
    }
}
