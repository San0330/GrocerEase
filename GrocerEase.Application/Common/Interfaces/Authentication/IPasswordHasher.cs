namespace GrocerEase.Application.Common.Interfaces.Authentication;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Checks a password against a stored hash. A <c>null</c> hash (no such user) is checked against a stand-in
    /// of the same cost and always returns <c>false</c>, so unknown and known accounts take the same time to answer.
    /// </summary>
    bool Verify(string password, string? hash);
}
