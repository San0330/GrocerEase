using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Routing;

namespace GrocerEase.Api.MiddleWare;

/// <summary>
/// Writes one structured line per request, after it has been answered: method, route, status, elapsed time and, for a
/// signed-in caller, the user id. It is the first middleware, so the status it logs is the one the client receives,
/// including the error bodies written further in.
/// </summary>
/// <remarks>
/// The line is deliberately thin. It carries the route <em>pattern</em> (<c>/products/{id}</c>), not the path, so the
/// same endpoint is one value however many ids pass through it and an id, a token or an address in the URL never
/// reaches the log. Headers, query strings and bodies are never read.
/// </remarks>
public sealed partial class RequestLoggingMiddleware
{
    // What a request that matched no endpoint is logged as. The raw path is attacker-controlled (scanners send
    // thousands of distinct ones), so it is neither a safe nor a useful value.
    private const string Unmatched = "unmatched";

    private const string HealthPrefix = "/health/";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            await _next(context);
        }
        catch
        {
            // ErrorHandlingMiddleware answers almost everything, so an exception reaching here means the response had
            // already started and could not be replaced. The client got a truncated reply: log it as a failure.
            Write(context, started, failed: true);
            throw;
        }

        Write(context, started, failed: false);
    }

    private void Write(HttpContext context, long started, bool failed)
    {
        var status = failed && !context.Response.HasStarted
            ? StatusCodes.Status500InternalServerError
            : context.Response.StatusCode;
        var route = RouteOf(context);
        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        var level = LevelFor(status, route, failed);
        if (!_logger.IsEnabled(level))
            return;

        var elapsed = Math.Round(elapsedMs, 3);
        var userId = UserIdOf(context);

        if (userId is null)
            RequestCompleted(_logger, level, context.Request.Method, route, status, elapsed);
        else
            AuthenticatedRequestCompleted(_logger, level, context.Request.Method, route, status, elapsed, userId);
    }

    private static LogLevel LevelFor(int status, string route, bool failed)
    {
        if (failed || status >= StatusCodes.Status500InternalServerError)
            return LogLevel.Error;

        // An orchestrator polls these every few seconds. A passing check is noise, so it is only written when asked for;
        // a failing one is the 5xx above and is always written.
        return route.StartsWith(HealthPrefix, StringComparison.Ordinal) ? LogLevel.Debug : LogLevel.Information;
    }

    private static string RouteOf(HttpContext context)
    {
        if (context.GetEndpoint() is not RouteEndpoint { RoutePattern.RawText: { Length: > 0 } pattern })
            return Unmatched;

        return pattern.StartsWith('/') ? pattern : "/" + pattern;
    }

    // Read after the request, because authentication runs inside the next delegate and sets the principal there.
    private static string? UserIdOf(HttpContext context) =>
        context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

    // Two messages rather than one with an optional user id: a property that is sometimes null shows up as "(null)"
    // in the text, and the structured field would be named after the parameter instead of the template.
    [LoggerMessage(
        EventId = 100,
        EventName = "RequestCompleted",
        Message = "{Method} {Route} responded {StatusCode} in {ElapsedMs} ms")]
    private static partial void RequestCompleted(
        ILogger logger,
        LogLevel level,
        string method,
        string route,
        int statusCode,
        double elapsedMs);

    [LoggerMessage(
        EventId = 101,
        EventName = "AuthenticatedRequestCompleted",
        Message = "{Method} {Route} responded {StatusCode} in {ElapsedMs} ms for user {UserId}")]
    private static partial void AuthenticatedRequestCompleted(
        ILogger logger,
        LogLevel level,
        string method,
        string route,
        int statusCode,
        double elapsedMs,
        string userId);
}
