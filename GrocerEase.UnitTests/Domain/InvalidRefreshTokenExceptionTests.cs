using GrocerEase.Domain.Exceptions;

namespace GrocerEase.UnitTests.Domain;

public class InvalidRefreshTokenExceptionTests
{
    [Fact]
    public void ExposesUnauthorizedProblemDetails()
    {
        var exception = new InvalidRefreshTokenException();

        Assert.Equal(ErrorKind.Unauthenticated, exception.Kind);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, exception.ErrorCode);
        Assert.Equal("Auth.InvalidRefreshToken", exception.ErrorCode);
        Assert.Equal("The refresh token is invalid or has expired.", exception.ErrorMessage);
        Assert.Equal(exception.ErrorMessage, exception.Message);
    }
}
