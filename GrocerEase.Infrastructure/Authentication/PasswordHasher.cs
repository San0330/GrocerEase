using GrocerEase.Application.Common.Interfaces.Authentication;
using BC = BCrypt.Net.BCrypt;

namespace GrocerEase.Infrastructure.Authentication;

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BC.HashPassword(password);

    public bool Verify(string password, string hash) => BC.Verify(password, hash);
}
