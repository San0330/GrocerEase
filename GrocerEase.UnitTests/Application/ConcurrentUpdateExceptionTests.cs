using GrocerEase.Application.Common.Exceptions;
using GrocerEase.Domain.Exceptions;

namespace GrocerEase.UnitTests.Application;

public class ConcurrentUpdateExceptionTests
{
    [Fact]
    public void ExposesConflictProblemDetails()
    {
        var exception = new ConcurrentUpdateException();

        Assert.Equal(ErrorKind.Conflict, exception.Kind);
        Assert.Equal(ConcurrentUpdateException.Code, exception.ErrorCode);
        Assert.Equal("Database.ConcurrentUpdate", exception.ErrorCode);
        Assert.Equal("Concurrent Update", ((IAppException)exception).Title);
        Assert.Equal("The resource was changed by another request. Try again.", exception.ErrorMessage);
        Assert.Equal(exception.ErrorMessage, exception.Message);
    }

    [Fact]
    public void KeepsTheOriginalFailureAsTheInnerException()
    {
        var inner = new InvalidOperationException("original");

        Assert.Same(inner, new ConcurrentUpdateException(inner).InnerException);
    }
}
