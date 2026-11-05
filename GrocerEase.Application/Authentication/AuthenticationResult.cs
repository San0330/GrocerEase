using System.Text;
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
        new(user.Id, user.FirstName, user.LastName, user.Email.Value, token);

    // A bearer token is a credential until it expires, and a record's generated ToString would print it.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Id = {Id}, FirstName = {FirstName}, LastName = {LastName}, Email = {Email}, Token = [redacted]");
        return true;
    }
}
