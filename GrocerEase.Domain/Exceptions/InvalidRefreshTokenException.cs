namespace GrocerEase.Domain.Exceptions;

/// <summary>
/// Thrown for a refresh token that is unknown, expired, or already used. One message covers all three, so the response
/// does not tell a caller which tokens exist.
/// </summary>
public class InvalidRefreshTokenException : Exception, IAppException
{
    private const string DefaultMessage = "The refresh token is invalid or has expired.";

    public ErrorKind Kind => ErrorKind.Unauthenticated;
    public string ErrorMessage => DefaultMessage;
    public string ErrorCode => ErrorCodes.InvalidRefreshToken;

    public InvalidRefreshTokenException()
        : base(DefaultMessage)
    {
    }
}
