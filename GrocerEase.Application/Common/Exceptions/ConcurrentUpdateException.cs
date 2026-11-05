using GrocerEase.Domain.Exceptions;

namespace GrocerEase.Application.Common.Exceptions;

/// <summary>A row changed between being read and being written, because another request saved it first.</summary>
public class ConcurrentUpdateException : Exception, IAppException
{
    public const string Code = "Database.ConcurrentUpdate";

    private const string DefaultMessage = "The resource was changed by another request. Try again.";

    public ErrorKind Kind => ErrorKind.Conflict;
    public string ErrorMessage => DefaultMessage;
    public string ErrorCode => Code;
    public string? Title => "Concurrent Update";

    public ConcurrentUpdateException(Exception? innerException = null)
        : base(DefaultMessage, innerException)
    {
    }
}
