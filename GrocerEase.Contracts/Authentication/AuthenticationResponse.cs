using System.Text;

namespace GrocerEase.Contracts.Authentication;

public record AuthenticationResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Token)
{
    // See AuthenticationResult: a record's generated ToString would print the bearer token.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Id = {Id}, FirstName = {FirstName}, LastName = {LastName}, Email = {Email}, Token = [redacted]");
        return true;
    }
}
