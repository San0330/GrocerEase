using GrocerEase.Application.Authentication;
using GrocerEase.Domain.Entities;

namespace GrocerEase.UnitTests.Application;

public class AuthenticationResultTests
{
    [Fact]
    public void From_CopiesUserFieldsAndToken()
    {
        var user = new User
        {
            Id = Guid.Parse("018f6b2e-7c3a-7a1b-8c2d-4e5f60718293"),
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Password = "hashed",
        };

        var result = AuthenticationResult.From(user, "signed-token");

        Assert.Equal(user.Id, result.Id);
        Assert.Equal("Ada", result.FirstName);
        Assert.Equal("Lovelace", result.LastName);
        Assert.Equal("ada@example.com", result.Email);
        Assert.Equal("signed-token", result.Token);
    }
}
