using System.Globalization;
using System.Text;

namespace GrocerEase.Contracts.Authentication;

public record AuthenticationResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Token,
    string RefreshToken)
{
    // See AuthenticationResult: a record's generated ToString would print both tokens.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Id = {Id}, FirstName = {FirstName}, LastName = {LastName}, Email = {Email}, Token = [redacted], RefreshToken = [redacted]");
        return true;
    }
}
