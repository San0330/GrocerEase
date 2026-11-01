namespace GrocerEase.Domain.Exceptions;

public class InvalidCredentialsException : Exception, IAppException
{
    private const string DefaultMessage = "Email or password is invalid.";

    public ErrorKind Kind => ErrorKind.Unauthenticated;
    public string ErrorMessage => DefaultMessage;
    public string ErrorCode => ErrorCodes.InvalidCredentials;

    public InvalidCredentialsException()
        : base(DefaultMessage)
    {
    }
}
