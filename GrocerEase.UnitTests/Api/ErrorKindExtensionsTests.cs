using GrocerEase.Api.Errors;
using GrocerEase.Domain.Exceptions;

namespace GrocerEase.UnitTests.Api;

public class ErrorKindExtensionsTests
{
    [Theory]
    [InlineData(ErrorKind.Invalid, 400)]
    [InlineData(ErrorKind.Unauthenticated, 401)]
    [InlineData(ErrorKind.Conflict, 409)]
    public void ToStatusCode_MapsEachKind(ErrorKind kind, int expected)
    {
        Assert.Equal(expected, kind.ToStatusCode());
    }

    [Fact]
    public void ToStatusCode_KnowsEveryKind_SoANewOneCannotBeForgotten()
    {
        foreach (var kind in Enum.GetValues<ErrorKind>())
        {
            var status = kind.ToStatusCode();
            Assert.InRange(status, 400, 499);
        }
    }
}
