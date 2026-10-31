using FluentValidation;
using GrocerEase.Contracts.Errors;
using GrocerEase.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace GrocerEase.Api.MiddleWare;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away; there is nobody to answer and nothing to alert on.
            _logger.LogDebug("Request was cancelled by the client.");
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            if (IsExpected(ex))
                _logger.LogInformation("Request rejected: {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
            else
                _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);

            await HandleExceptionAsync(context, ex);
        }
    }

    // Validation failures and typed domain errors below 500 are caller mistakes, not server faults.
    private static bool IsExpected(Exception exception) =>
        exception is ValidationException or IAppException { StatusCode: < 500 } or BadHttpRequestException { StatusCode: < 500 };

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = RequestTrace.IdFor(context);

        if (exception is ValidationException validationEx)
        {
            var errors = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            var validationProblem = new ValidationErrorResponse(
                ErrorCodes.ValidationFailed,
                StatusCodes.Status422UnprocessableEntity,
                "Validation Failed",
                errors,
                traceId);

            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await context.Response.WriteAsJsonAsync(validationProblem, options: null, contentType: "application/problem+json");
            return;
        }

        var (statusCode, title, detail, code) = exception switch
        {
            IAppException appEx
                => (appEx.StatusCode,
                    appEx.Title ?? ReasonPhrase(appEx.StatusCode),
                    appEx.ErrorMessage,
                    appEx.ErrorCode),

            // Kestrel rejected the request while the body was being read: too large, too slow, or broken framing.
            // Left alone it would fall through to the 500 below, hiding the 4xx Kestrel chose.
            BadHttpRequestException badRequest
                => (badRequest.StatusCode,
                    ReasonPhrase(badRequest.StatusCode),
                    "The request could not be read.",
                    ErrorCodes.MalformedRequest),

            DbUpdateException { InnerException: { } inner } when inner.Message.Contains("23505")
                => (StatusCodes.Status409Conflict,
                    "Duplicate Email",
                    "A user with this email already exists.",
                    ErrorCodes.DuplicateEmail),

            DbUpdateException
                => (StatusCodes.Status400BadRequest,
                    "Database Error",
                    "A database constraint was violated.",
                    ErrorCodes.DatabaseConstraint),

            _   => (StatusCodes.Status500InternalServerError,
                    "Internal Server Error",
                    "An unexpected error occurred.",
                    ErrorCodes.Unexpected)
        };

        var problem = new ErrorResponse(code, statusCode, title, detail, traceId);

        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        408 => "Request Timeout",
        409 => "Conflict",
        413 => "Content Too Large",
        415 => "Unsupported Media Type",
        422 => "Unprocessable Entity",
        _   => "Error"
    };
}
