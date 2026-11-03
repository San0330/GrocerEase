using System.Text;

namespace GrocerEase.Application.Authentication.Commands;

public record RegisterCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password)
{
    // The ToString a record generates prints every property, so logging or interpolating a command would write the
    // plaintext password out. Everything else is still printed; PasswordRedactionTests keeps it that way.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"FirstName = {FirstName}, LastName = {LastName}, Email = {Email}, Password = [redacted]");
        return true;
    }
}
