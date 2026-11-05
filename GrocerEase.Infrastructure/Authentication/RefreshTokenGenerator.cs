using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using GrocerEase.Application.Common.Interfaces.Authentication;
using GrocerEase.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace GrocerEase.Infrastructure.Authentication;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    // 256 bits, the strength of the JWT signing key, so guessing one is out of reach.
    private const int TokenBytes = 32;

    private readonly JwtSettings _settings;
    private readonly IDateTimeProvider _clock;

    public RefreshTokenGenerator(IOptions<JwtSettings> settings, IDateTimeProvider clock)
    {
        _settings = settings.Value;
        _clock = clock;
    }

    public IssuedRefreshToken Issue()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        return new IssuedRefreshToken(token, Hash(token), _clock.UtcNow.AddDays(_settings.RefreshTokenExpiryDays));
    }

    // A fast hash is right here. A token is 256 random bits, so there is nothing to guess and no benefit from
    // BCrypt's slowness, which exists for human-chosen passwords.
    public string Hash(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }
}
