namespace GrocerEase.Domain.Exceptions;

public class InvalidCredentialsException : Exception, IAppException
{
    private const string DefaultMessage = "Email or password is invalid.";

    public int StatusCode => 401;
    public string ErrorMessage => DefaultMessage;

    public InvalidCredentialsException()
        : base(DefaultMessage)
    {
    }
}
