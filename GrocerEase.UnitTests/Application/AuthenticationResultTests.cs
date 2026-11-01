using GrocerEase.Application.Authentication;
using GrocerEase.Domain.Entities;
using GrocerEase.Domain.ValueObjects;

namespace GrocerEase.UnitTests.Application;

public class AuthenticationResultTests
{
    [Fact]
    public void From_CopiesUserFieldsAndToken()
    {
        var user = User.Register(
            "Ada",
            "Lovelace",
            Email.Create("Ada@Example.com"),
            "hashed",
            new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc));

        var result = AuthenticationResult.From(user, "signed-token");

        Assert.Equal(user.Id, result.Id);
        Assert.Equal("Ada", result.FirstName);
        Assert.Equal("Lovelace", result.LastName);
        Assert.Equal("ada@example.com", result.Email);
        Assert.Equal("signed-token", result.Token);
    }
}
