using System.Text;
using GrocerEase.Domain.Entities;

namespace GrocerEase.Application.Authentication;

public record AuthenticationResult(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Token,
    string RefreshToken)
{
    public static AuthenticationResult From(User user, string token, string refreshToken) =>
        new(user.Id, user.FirstName, user.LastName, user.Email.Value, token, refreshToken);

    // A bearer token and a refresh token are credentials until it expires, and a record's generated ToString would print it.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Id = {Id}, FirstName = {FirstName}, LastName = {LastName}, Email = {Email}, Token = [redacted], RefreshToken = [redacted]");
        return true;
    }
}
