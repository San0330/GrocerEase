using GrocerEase.Application.Common.Interfaces.Authentication;
using Microsoft.Extensions.Options;
using BC = BCrypt.Net.BCrypt;

namespace GrocerEase.Infrastructure.Authentication;

public class PasswordHasher : IPasswordHasher
{
    private readonly int _cost;
    private readonly string _standInHash;

    public PasswordHasher(IOptions<PasswordHashingSettings> options)
    {
        _cost = options.Value.Cost;

        // Hashed once, at the current cost, so checking against it costs what checking a real user costs.
        _standInHash = BC.HashPassword(Guid.NewGuid().ToString("N"), _cost, enhancedEntropy: false);
    }

    public string Hash(string password) => BC.HashPassword(password, _cost, enhancedEntropy: false);

    public bool Verify(string password, string? hash)
    {
        if (hash is null)
        {
            BC.Verify(password, _standInHash);
            return false;
        }

        return BC.Verify(password, hash);
    }
}
