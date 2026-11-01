namespace GrocerEase.Api.Errors;

/// <summary>Codes for failures that exist only because of how the API is called. Clients branch on these strings, so never change one.</summary>
public static class ApiErrorCodes
{
    public const string MalformedRequest = "Request.Malformed";
    public const string ValidationFailed = "Validation.Failed";
    public const string Unauthorized = "Auth.Unauthorized";
    public const string RateLimited = "RateLimit.Exceeded";
    public const string Unexpected = "Server.Unexpected";
}
