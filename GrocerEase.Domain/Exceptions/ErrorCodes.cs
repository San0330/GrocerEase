namespace GrocerEase.Domain.Exceptions;

public static class ErrorCodes
{
    public const string InvalidCredentials = "User.InvalidCredentials";
    public const string DuplicateEmail = "User.DuplicateEmail";
    public const string DatabaseConstraint = "Database.ConstraintViolation";
    public const string Unauthorized = "Auth.Unauthorized";
    public const string Unexpected = "Server.Unexpected";
    public const string ValidationFailed = "Validation.Failed";
    public const string MalformedRequest = "Request.Malformed";
    public const string RateLimited = "RateLimit.Exceeded";
}
