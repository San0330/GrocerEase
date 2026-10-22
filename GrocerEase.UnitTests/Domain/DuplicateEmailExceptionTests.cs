using GrocerEase.Domain.Exceptions;

namespace GrocerEase.UnitTests.Domain;

public class DuplicateEmailExceptionTests
{
    [Fact]
    public void ExposesConflictProblemDetails()
    {
        var exception = new DuplicateEmailException();

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(ErrorCodes.DuplicateEmail, exception.ErrorCode);
        Assert.Equal("A user with this email already exists.", exception.ErrorMessage);
        Assert.Equal(exception.ErrorMessage, exception.Message);
    }
}
