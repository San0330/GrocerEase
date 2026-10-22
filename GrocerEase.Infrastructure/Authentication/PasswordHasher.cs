using GrocerEase.Application.Common.Interfaces.Authentication;
using Microsoft.Extensions.Options;
using BC = BCrypt.Net.BCrypt;

namespace GrocerEase.Infrastructure.Authentication;

public class PasswordHasher : IPasswordHasher
{
    private readonly int _cost;

    public PasswordHasher(IOptions<PasswordHashingSettings> options)
    {
        _cost = options.Value.Cost;
    }

    public string Hash(string password) => BC.HashPassword(password, _cost, enhancedEntropy: false);

    public bool Verify(string password, string hash) => BC.Verify(password, hash);
}
