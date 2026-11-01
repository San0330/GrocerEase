namespace GrocerEase.Api.Errors;

/// <summary>Codes for failures that exist only because of how the API is called. Clients branch on these strings, so never change one.</summary>
public static class ApiErrorCodes
{
    public const string MalformedRequest = "Request.Malformed";
    public const string ValidationFailed = "Validation.Failed";
    public const string Unauthorized = "Auth.Unauthorized";
    public const string RateLimited = "RateLimit.Exceeded";
    public const string Forbidden = "Auth.Forbidden";
    public const string NotFound = "Request.NotFound";
    public const string MethodNotAllowed = "Request.MethodNotAllowed";
    public const string NotAcceptable = "Request.NotAcceptable";
    public const string UnsupportedMediaType = "Request.UnsupportedMediaType";

    /// <summary>Any other 4xx the framework produced without one of the codes above.</summary>
    public const string Rejected = "Request.Rejected";

    public const string Unexpected = "Server.Unexpected";
}
