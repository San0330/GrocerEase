using GrocerEase.Contracts.Errors;
using Microsoft.AspNetCore.Diagnostics;

namespace GrocerEase.Api.Errors;

/// <summary>
/// Gives a body to responses the framework ends with only a status code: an unknown route (404), a wrong method (405),
/// an unsupported content type (415), a bare 401 or 403. Without it those answer with an empty body or the framework's
/// own shape, and a client would need a second parser for them.
/// Wired through <c>UseStatusCodePages</c>, which only runs for a 400-599 response that has no body yet.
/// </summary>
public static class StatusCodeProblem
{
    public static Task WriteAsync(StatusCodeContext statusCodeContext)
    {
        var context = statusCodeContext.HttpContext;

        // Nobody is listening if the client already went away (the exception middleware answers that with 499).
        if (context.RequestAborted.IsCancellationRequested)
            return Task.CompletedTask;

        var status = context.Response.StatusCode;
        var (code, detail) = Describe(status);

        return ApiProblem.WriteAsync(context, ApiProblem.Error(context, code, status, ApiProblem.ReasonPhrase(status), detail));
    }

    public static (string Code, string Detail) Describe(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => (ApiErrorCodes.Unauthorized, UnauthorizedResponse.Detail),
        StatusCodes.Status403Forbidden => (ApiErrorCodes.Forbidden, "You do not have permission to perform this action."),
        StatusCodes.Status404NotFound => (ApiErrorCodes.NotFound, "The requested resource was not found."),
        StatusCodes.Status405MethodNotAllowed => (ApiErrorCodes.MethodNotAllowed, "The HTTP method is not allowed for this resource."),
        StatusCodes.Status406NotAcceptable => (ApiErrorCodes.NotAcceptable, "The API can only respond with application/json."),
        StatusCodes.Status415UnsupportedMediaType => (ApiErrorCodes.UnsupportedMediaType, "The request body must be sent as application/json."),
        >= 500 => (ApiErrorCodes.Unexpected, "An unexpected error occurred."),
        _ => (ApiErrorCodes.Rejected, "The request could not be processed."),
    };
}
