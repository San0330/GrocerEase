using System.Text.Json;
using GrocerEase.Api.Errors;
using GrocerEase.Domain.Exceptions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GrocerEase.UnitTests.Api;

public class UnauthorizedResponseTests
{
    private static readonly IServiceProvider Services = new ServiceCollection().AddLogging().BuildServiceProvider();

    [Fact]
    public async Task WriteAsync_WithNoToken_WritesTheUnauthorizedProblemAndAsksForABearer()
    {
        var (context, challenge) = ChallengeWith(authenticateFailure: null);

        await UnauthorizedResponse.WriteAsync(challenge);

        Assert.True(challenge.Handled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.Equal("Bearer", context.Response.Headers.WWWAuthenticate.ToString());

        var body = await ReadBodyAsync(context);
        Assert.Equal(ApiErrorCodes.Unauthorized, body.GetProperty("code").GetString());
        Assert.Equal(401, body.GetProperty("status").GetInt32());
        Assert.Equal("Unauthorized", body.GetProperty("title").GetString());
        Assert.Equal("http-trace", body.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task WriteAsync_WhenATokenWasRejected_FlagsItAsInvalidWithoutSayingWhy()
    {
        var (context, challenge) = ChallengeWith(authenticateFailure: new Exception("IDX10223: Lifetime validation failed."));

        await UnauthorizedResponse.WriteAsync(challenge);

        Assert.Equal("Bearer error=\"invalid_token\"", context.Response.Headers.WWWAuthenticate.ToString());
        var body = await ReadBodyAsync(context);
        Assert.DoesNotContain("IDX10223", body.ToString());
    }

    private static (DefaultHttpContext Context, JwtBearerChallengeContext Challenge) ChallengeWith(Exception? authenticateFailure)
    {
        var context = new DefaultHttpContext { RequestServices = Services, TraceIdentifier = "http-trace" };
        context.Response.Body = new MemoryStream();

        var challenge = new JwtBearerChallengeContext(
            context,
            new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)),
            new JwtBearerOptions(),
            new AuthenticationProperties())
        {
            AuthenticateFailure = authenticateFailure,
        };

        return (context, challenge);
    }

    private static async Task<JsonElement> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return (await JsonDocument.ParseAsync(context.Response.Body)).RootElement;
    }
}
