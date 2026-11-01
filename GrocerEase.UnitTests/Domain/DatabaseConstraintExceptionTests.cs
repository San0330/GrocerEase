using GrocerEase.Domain.Exceptions;

namespace GrocerEase.UnitTests.Domain;

public class DatabaseConstraintExceptionTests
{
    [Fact]
    public void ExposesBadRequestProblemDetails()
    {
        var exception = new DatabaseConstraintException();

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(ErrorCodes.DatabaseConstraint, exception.ErrorCode);
        Assert.Equal("Database Error", ((IAppException)exception).Title);
        Assert.Equal("A database constraint was violated.", exception.ErrorMessage);
        Assert.Equal(exception.ErrorMessage, exception.Message);
    }

    [Fact]
    public void KeepsTheOriginalFailureAsTheInnerException()
    {
        var inner = new Exception("original");

        Assert.Same(inner, new DatabaseConstraintException(inner).InnerException);
    }
}
