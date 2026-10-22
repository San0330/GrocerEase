using GrocerEase.Domain.Exceptions;

namespace GrocerEase.UnitTests.Domain;

public class InvalidCredentialsExceptionTests
{
    [Fact]
    public void ExposesUnauthorizedProblemDetails()
    {
        var exception = new InvalidCredentialsException();

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, exception.ErrorCode);
        Assert.Equal("Email or password is invalid.", exception.ErrorMessage);
        Assert.Equal(exception.ErrorMessage, exception.Message);
    }
}
