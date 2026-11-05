using Microsoft.Extensions.Logging;

namespace GrocerEase.Application.Authentication;

/// <summary>
/// The authentication events worth keeping, with fixed ids so a query or an alert can name one. Each carries only ids,
/// an outcome and, for a refusal, a reason. Never pass a password, a JWT, a refresh token or an email address here:
/// <see cref="EmailFingerprint"/> stands in for the address.
/// </summary>
/// <remarks>
/// The reasons are for the operator. The response to the caller never says which one applied.
/// </remarks>
internal static partial class AuthenticationLog
{
    public static class Reasons
    {
        public const string UnknownEmail = "unknown_email";
        public const string WrongPassword = "wrong_password";
        public const string UnknownToken = "unknown_token";
        public const string ExpiredToken = "expired_token";
        public const string UserMissing = "user_missing";
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "User {UserId} registered")]
    public static partial void UserRegistered(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Registration rejected: the email is already registered ({EmailFingerprint})")]
    public static partial void RegistrationRejected(this ILogger logger, string emailFingerprint);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "User {UserId} logged in")]
    public static partial void LoginSucceeded(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "Login failed: {Reason} ({EmailFingerprint})")]
    public static partial void LoginFailed(this ILogger logger, string reason, string emailFingerprint);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "User {UserId} refreshed a token (family {FamilyId})")]
    public static partial void TokenRefreshed(this ILogger logger, Guid userId, Guid familyId);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Refresh rejected: {Reason}")]
    public static partial void RefreshRejected(this ILogger logger, string reason);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Warning,
        Message = "Refresh token reuse detected for user {UserId} (family {FamilyId}); {TokensRevoked} token(s) revoked")]
    public static partial void RefreshTokenReuseDetected(this ILogger logger, Guid userId, Guid familyId, int tokensRevoked);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Information,
        Message = "User {UserId} logged out (family {FamilyId})")]
    public static partial void LoggedOut(this ILogger logger, Guid userId, Guid familyId);
}
