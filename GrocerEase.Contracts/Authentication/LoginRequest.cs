using System.Globalization;
using System.Text;

namespace GrocerEase.Contracts.Authentication;

public record LoginRequest(
    string Email,
    string Password)
{
    // See RegisterRequest: a record's generated ToString would print the plaintext password.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Email = {Email}, Password = [redacted]");
        return true;
    }
}
