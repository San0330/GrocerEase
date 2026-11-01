using GrocerEase.Api.MiddleWare;
using GrocerEase.Contracts.Errors;

namespace GrocerEase.Api.Errors;

/// <summary>
/// The one place an error response is built and written. The exception middleware, the bearer challenge, the rate
/// limiter, model binding, and the status-code pages all go through here, so every failure has the same shape and
/// content type: <c>application/problem+json</c> (RFC 9457) with <c>code</c> and <c>traceId</c> as extension members.
/// </summary>
public static class ApiProblem
{
    public const string ContentType = "application/problem+json";

    public static ErrorResponse Error(HttpContext context, string code, int status, string title, string detail) =>
        new(code, status, title, detail, RequestTrace.IdFor(context));

    public static ValidationErrorResponse Validation(
        HttpContext context,
        string code,
        int status,
        string title,
        IReadOnlyDictionary<string, string[]> errors) =>
        new(code, status, title, errors, RequestTrace.IdFor(context));

    public static Task WriteAsync(HttpContext context, ErrorResponse body, CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = body.Status;
        return context.Response.WriteAsJsonAsync(body, options: null, ContentType, cancellationToken);
    }

    public static Task WriteAsync(HttpContext context, ValidationErrorResponse body, CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = body.Status;
        return context.Response.WriteAsJsonAsync(body, options: null, ContentType, cancellationToken);
    }

    public static string ReasonPhrase(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        406 => "Not Acceptable",
        408 => "Request Timeout",
        409 => "Conflict",
        413 => "Content Too Large",
        415 => "Unsupported Media Type",
        422 => "Unprocessable Entity",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        _ => "Error"
    };
}
