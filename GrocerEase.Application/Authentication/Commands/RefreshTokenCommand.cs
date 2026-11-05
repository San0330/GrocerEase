using System.Text;

namespace GrocerEase.Application.Authentication.Commands;

public record RefreshTokenCommand(string RefreshToken)
{
    // A refresh token is a credential until it expires, and a record's generated ToString would print it.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append("RefreshToken = [redacted]");
        return true;
    }
}
