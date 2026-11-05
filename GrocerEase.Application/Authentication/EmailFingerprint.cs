using System.Security.Cryptography;
using System.Text;

namespace GrocerEase.Application.Authentication;

/// <summary>
/// A short, stable stand-in for an email address in the log. Repeated attempts against one account show up as the same
/// fingerprint, so an operator can see them, while the address itself, which is personal data, is never written.
/// </summary>
public static class EmailFingerprint
{
    // 8 bytes is plenty to tell accounts apart in a log and too little to be worth reversing.
    private const int Bytes = 8;

    public static string Of(string email)
    {
        // Lower-cased like Email.Create does, so "Ada@Example.com" and "ada@example.com" are one account.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexStringLower(hash.AsSpan(0, Bytes));
    }
}
