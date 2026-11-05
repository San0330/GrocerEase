using System.Text;

namespace GrocerEase.Application.Authentication.Commands;

public record LogoutCommand(string RefreshToken)
{
    // See RefreshTokenCommand: a record's generated ToString would print the refresh token.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append("RefreshToken = [redacted]");
        return true;
    }
}
