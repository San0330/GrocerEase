using System.Globalization;
using System.Text;

namespace GrocerEase.Application.Common.Interfaces.Authentication;

public interface IRefreshTokenGenerator
{
    /// <summary>Makes a new random token. Only <see cref="IssuedRefreshToken.Hash"/> is stored; the token goes to the caller once.</summary>
    IssuedRefreshToken Issue();

    /// <summary>Hashes a token the caller presented, to look it up. The same token always gives the same hash.</summary>
    string Hash(string refreshToken);
}

/// <summary>A refresh token as the caller receives it, with the hash to store and the time it stops working.</summary>
public record IssuedRefreshToken(string Token, string Hash, DateTime ExpiresAt)
{
    // The token is a credential until it expires, and a record's generated ToString would print it.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Token = [redacted], Hash = {Hash}, ExpiresAt = {ExpiresAt:O}");
        return true;
    }
}
