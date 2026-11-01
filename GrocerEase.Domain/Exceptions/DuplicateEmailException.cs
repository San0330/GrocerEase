namespace GrocerEase.Domain.Exceptions;

public class DuplicateEmailException : Exception, IAppException
{
    private const string DefaultMessage = "A user with this email already exists.";

    public ErrorKind Kind => ErrorKind.Conflict;
    public string ErrorMessage => DefaultMessage;
    public string ErrorCode => ErrorCodes.DuplicateEmail;
    public string? Title => "Duplicate Email";

    public DuplicateEmailException(Exception? innerException = null)
        : base(DefaultMessage, innerException)
    {
    }
}
