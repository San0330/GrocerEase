using System.Globalization;
using System.Text;

namespace GrocerEase.Application.Authentication.Commands;

public record LoginCommand(
    string Email,
    string Password)
{
    // See RegisterCommand: a record's generated ToString would print the plaintext password.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Email = {Email}, Password = [redacted]");
        return true;
    }
}
