using System.Security.Claims;
using GrocerEase.Api.MiddleWare;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace GrocerEase.UnitTests.Api;

public class RequestLoggingMiddlewareTests
{
    private readonly FakeLogger<RequestLoggingMiddleware> _logger = new();

    [Fact]
    public async Task WritesOneLineAfterTheRequestWithMethodRouteStatusAndElapsedTime()
    {
        var context = ContextFor("POST", "/auth/login", pattern: "auth/login");

        await InvokeAsync(context, c => c.Response.StatusCode = StatusCodes.Status200OK);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("POST", State(record, "Method"));
        Assert.Equal("/auth/login", State(record, "Route"));
        Assert.Equal("200", State(record, "StatusCode"));
        Assert.True(double.Parse(State(record, "ElapsedMs")!, System.Globalization.CultureInfo.InvariantCulture) >= 0);
    }

    [Fact]
    public async Task LogsTheRoutePatternAndNotThePathOrTheQueryString()
    {
        var context = ContextFor("GET", "/products/42", pattern: "products/{id}");
        context.Request.QueryString = new QueryString("?token=secret-value&email=ada@example.com");

        await InvokeAsync(context);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal("/products/{id}", State(record, "Route"));
        Assert.DoesNotContain("42", record.Message);
        Assert.DoesNotContain("secret-value", record.Message);
        Assert.DoesNotContain("ada@example.com", record.Message);
        Assert.All(record.StructuredState!, pair => Assert.DoesNotContain("secret-value", pair.Value));
    }

    [Fact]
    public async Task ARequestThatMatchedNoRoute_IsLoggedAsUnmatchedWithoutItsPath()
    {
        var context = ContextFor("GET", "/wp-login.php?x=1", pattern: null);

        await InvokeAsync(context, c => c.Response.StatusCode = StatusCodes.Status404NotFound);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal("unmatched", State(record, "Route"));
        Assert.DoesNotContain("wp-login", record.Message);
    }

    [Theory]
    [InlineData(200, LogLevel.Information)]
    [InlineData(204, LogLevel.Information)]
    [InlineData(401, LogLevel.Information)]
    [InlineData(429, LogLevel.Information)]
    [InlineData(500, LogLevel.Error)]
    [InlineData(503, LogLevel.Error)]
    public async Task TheLevelFollowsTheStatus(int status, LogLevel expected)
    {
        var context = ContextFor("GET", "/profile", pattern: "profile");

        await InvokeAsync(context, c => c.Response.StatusCode = status);

        Assert.Equal(expected, Assert.Single(_logger.Collector.GetSnapshot()).Level);
    }

    [Theory]
    [InlineData("health/live")]
    [InlineData("health/ready")]
    public async Task ASuccessfulHealthCheck_IsLoggedAtDebug_SoOrchestratorPollingDoesNotFillTheLog(string pattern)
    {
        var context = ContextFor("GET", "/" + pattern, pattern);

        await InvokeAsync(context);

        Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Collector.GetSnapshot()).Level);
    }

    [Fact]
    public async Task AFailingHealthCheck_IsStillLoggedAsAnError()
    {
        var context = ContextFor("GET", "/health/ready", pattern: "health/ready");

        await InvokeAsync(context, c => c.Response.StatusCode = StatusCodes.Status503ServiceUnavailable);

        Assert.Equal(LogLevel.Error, Assert.Single(_logger.Collector.GetSnapshot()).Level);
    }

    [Fact]
    public async Task ForAnAuthenticatedRequest_LogsTheUserIdFromTheSubClaim()
    {
        var context = ContextFor("GET", "/profile", pattern: "profile");
        var userId = Guid.NewGuid().ToString();

        await InvokeAsync(context, c => c.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim("sub", userId)], authenticationType: "Bearer")));

        // The principal is set by authentication, which runs inside the next delegate, so it is read afterwards.
        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(userId, State(record, "UserId"));
        Assert.Contains(userId, record.Message);
    }

    [Fact]
    public async Task ForAnAnonymousRequest_LogsNoUserId()
    {
        var context = ContextFor("POST", "/auth/login", pattern: "auth/login");

        await InvokeAsync(context);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Null(State(record, "UserId"));
        Assert.DoesNotContain("null", record.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NeverLogsHeadersOrTheBody()
    {
        var context = ContextFor("POST", "/auth/login", pattern: "auth/login");
        context.Request.Headers.Authorization = "Bearer eyJ-super-secret-token";
        context.Request.Headers["Cookie"] = "session=secret-cookie";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream("""{"password":"Passw0rd!"}"""u8.ToArray());

        await InvokeAsync(context);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        var everything = record.Message + string.Join(' ', record.StructuredState!.Select(p => p.Value));
        Assert.DoesNotContain("eyJ-super-secret-token", everything);
        Assert.DoesNotContain("secret-cookie", everything);
        Assert.DoesNotContain("Passw0rd!", everything);
    }

    [Fact]
    public async Task WhenTheRestOfThePipelineThrows_LogsAnErrorAndRethrows()
    {
        var context = ContextFor("GET", "/profile", pattern: "profile");
        var failure = new InvalidOperationException("failed halfway");
        var middleware = new RequestLoggingMiddleware(_ => throw failure, _logger);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Same(failure, thrown);
        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal("500", State(record, "StatusCode"));
    }

    [Fact]
    public async Task CallsTheNextDelegateExactlyOnce()
    {
        var calls = 0;
        var middleware = new RequestLoggingMiddleware(_ => { calls++; return Task.CompletedTask; }, _logger);

        await middleware.InvokeAsync(ContextFor("GET", "/profile", pattern: "profile"));

        Assert.Equal(1, calls);
    }

    private Task InvokeAsync(DefaultHttpContext context, Action<DefaultHttpContext>? next = null) =>
        new RequestLoggingMiddleware(
            _ =>
            {
                next?.Invoke(context);
                return Task.CompletedTask;
            },
            _logger).InvokeAsync(context);

    private static DefaultHttpContext ContextFor(string method, string path, string? pattern)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        var query = path.IndexOf('?');
        context.Request.Path = query < 0 ? path : path[..query];
        if (query >= 0)
            context.Request.QueryString = new QueryString(path[query..]);

        if (pattern is not null)
        {
            context.SetEndpoint(new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse(pattern),
                order: 0,
                EndpointMetadataCollection.Empty,
                displayName: pattern));
        }

        return context;
    }

    private static string? State(Microsoft.Extensions.Logging.Testing.FakeLogRecord record, string key) =>
        record.StructuredState!.SingleOrDefault(pair => pair.Key == key).Value;
}
