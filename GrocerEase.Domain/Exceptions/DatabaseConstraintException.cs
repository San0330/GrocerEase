namespace GrocerEase.Domain.Exceptions;

/// <summary>A write was rejected by a database constraint that has no more specific exception.</summary>
public class DatabaseConstraintException : Exception, IAppException
{
    private const string DefaultMessage = "A database constraint was violated.";

    public int StatusCode => 400;
    public string ErrorMessage => DefaultMessage;
    public string ErrorCode => ErrorCodes.DatabaseConstraint;
    public string? Title => "Database Error";

    public DatabaseConstraintException(Exception? innerException = null)
        : base(DefaultMessage, innerException)
    {
    }
}
