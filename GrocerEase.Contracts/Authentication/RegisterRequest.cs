using System.Text;

namespace GrocerEase.Contracts.Authentication;

public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password)
{
    // A record's generated ToString prints every property, which would write the plaintext password to anything that
    // logs or interpolates the request. JSON binding and serialisation do not use ToString, so the contract is unchanged.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"FirstName = {FirstName}, LastName = {LastName}, Email = {Email}, Password = [redacted]");
        return true;
    }
}
