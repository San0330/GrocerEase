using System.Text;

namespace GrocerEase.Contracts.Authentication;

public record RefreshRequest(string RefreshToken)
{
    // See AuthenticationResponse: a record's generated ToString would print the refresh token.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append("RefreshToken = [redacted]");
        return true;
    }
}
