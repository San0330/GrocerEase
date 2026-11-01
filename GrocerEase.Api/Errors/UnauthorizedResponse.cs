using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace GrocerEase.Api.Errors;

/// <summary>
/// Answers a request that reached a protected endpoint without a valid bearer token. The challenge is issued by
/// the JwtBearer handler, outside the controllers and before <c>ErrorHandlingMiddleware</c> could see it, so on its
/// own it would send an empty 401 and not the API's error shape.
/// </summary>
public static class UnauthorizedResponse
{
    public const string Detail = "Authentication is required, or the bearer token is missing, expired, or not valid.";

    public static async Task WriteAsync(JwtBearerChallengeContext context)
    {
        // Without this the handler goes on to write its own empty response after this event returns.
        context.HandleResponse();

        var response = context.Response;

        // RFC 6750: say which scheme to use, and flag a token that was sent but rejected. The reason is left out
        // so the response does not tell a caller whether a token expired or its signature failed.
        response.Headers.WWWAuthenticate = context.AuthenticateFailure is null
            ? JwtBearerDefaults.AuthenticationScheme
            : $"{JwtBearerDefaults.AuthenticationScheme} error=\"invalid_token\"";

        await ApiProblem.WriteAsync(
            context.HttpContext,
            ApiProblem.Error(
                context.HttpContext,
                ApiErrorCodes.Unauthorized,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                Detail));
    }
}
