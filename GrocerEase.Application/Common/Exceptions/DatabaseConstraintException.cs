using GrocerEase.Domain.Exceptions;

namespace GrocerEase.Application.Common.Exceptions;

/// <summary>A write was rejected by a database constraint that has no more specific exception.</summary>
public class DatabaseConstraintException : Exception, IAppException
{
    public const string Code = "Database.ConstraintViolation";

    private const string DefaultMessage = "A database constraint was violated.";

    public ErrorKind Kind => ErrorKind.Invalid;
    public string ErrorMessage => DefaultMessage;
    public string ErrorCode => Code;
    public string? Title => "Database Error";

    public DatabaseConstraintException(Exception? innerException = null)
        : base(DefaultMessage, innerException)
    {
    }
}
