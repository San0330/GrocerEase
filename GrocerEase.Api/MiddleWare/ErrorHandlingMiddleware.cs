using Microsoft.AspNetCore.Mvc;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail) = exception switch
        {
            DbUpdateException { InnerException: { } inner } when inner.Message.Contains("23505")
                => (StatusCodes.Status409Conflict,
                    "Duplicate Email",
                    "A user with this email already exists."),

            DbUpdateException
                => (StatusCodes.Status400BadRequest,
                    "Database Error",
                    "A database constraint was violated."),

            _   => (StatusCodes.Status500InternalServerError,
                    "Internal Server Error",
                    "An unexpected error occurred.")
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(problem);
    }
}
