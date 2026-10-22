using GrocerEase.Domain.Entities;

namespace GrocerEase.Application.Authentication;

public record AuthenticationResult(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Token)
{
    public static AuthenticationResult From(User user, string token) =>
        new(user.Id, user.FirstName, user.LastName, user.Email, token);
}
