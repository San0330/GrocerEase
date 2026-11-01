using FluentValidation;
using GrocerEase.Api.Errors;
using GrocerEase.Contracts.Errors;
using GrocerEase.Domain.Exceptions;

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
        catch (Exception ex) when (context.Response.HasStarted)
        {
            // Status and headers are already on the wire, so an error body cannot be written. Rethrowing makes the
            // server abort the connection, which tells the client the response is incomplete.
            _logger.LogError(ex, "Unhandled exception after the response had started: {Message}", ex.Message);
            throw;
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

    // Validation failures and typed domain errors are caller mistakes, not server faults.
    private static bool IsExpected(Exception exception) =>
        exception is ValidationException or IAppException or BadHttpRequestException { StatusCode: < 500 };

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (exception is ValidationException validationEx)
        {
            var errors = validationEx.Errors
                .GroupBy(e => ErrorKeys.ToJsonName(e.PropertyName))
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            await ApiProblem.WriteAsync(
                context,
                ApiProblem.Validation(
                    context,
                    ApiErrorCodes.ValidationFailed,
                    StatusCodes.Status422UnprocessableEntity,
                    "Validation Failed",
                    errors));
            return;
        }

        var (statusCode, title, detail, code) = exception switch
        {
            IAppException appEx
                => (appEx.Kind.ToStatusCode(),
                    appEx.Title ?? ApiProblem.ReasonPhrase(appEx.Kind.ToStatusCode()),
                    appEx.ErrorMessage,
                    appEx.ErrorCode),

            // Kestrel rejected the request while the body was being read: too large, too slow, or broken framing.
            // Left alone it would fall through to the 500 below, hiding the 4xx Kestrel chose.
            BadHttpRequestException badRequest
                => (badRequest.StatusCode,
                    ApiProblem.ReasonPhrase(badRequest.StatusCode),
                    "The request could not be read.",
                    ApiErrorCodes.MalformedRequest),

            // Anything else, including a database failure that Infrastructure did not translate, is our fault.
            _   => (StatusCodes.Status500InternalServerError,
                    "Internal Server Error",
                    "An unexpected error occurred.",
                    ApiErrorCodes.Unexpected)
        };

        await ApiProblem.WriteAsync(context, ApiProblem.Error(context, code, statusCode, title, detail));
    }
}
