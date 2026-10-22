using System.IdentityModel.Tokens.Jwt;
using System.Text;
using GrocerEase.Infrastructure.Authentication;
using GrocerEase.UnitTests.Support;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GrocerEase.UnitTests.Infrastructure;

public class JwtTokenGeneratorTests
{
    private const string Secret = "unit-test-secret-key-at-least-32b";
    private static readonly DateTime Now = new(2026, 10, 23, 8, 30, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.Parse("018f6b2e-7c3a-7a1b-8c2d-4e5f60718293");

    private readonly JwtTokenGenerator _sut = new(
        Options.Create(new JwtSettings
        {
            Secret = Secret,
            Issuer = "grocerease",
            Audience = "grocerease-clients",
            ExpiryMinutes = 15,
        }),
        new FixedDateTimeProvider(Now));

    [Fact]
    public void GenerateToken_SignsATokenWithTheConfiguredClaimsAndExpiry()
    {
        var token = _sut.GenerateToken(UserId, "Ada", "Lovelace", "ada@example.com");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("grocerease", jwt.Issuer);
        Assert.Equal("grocerease-clients", Assert.Single(jwt.Audiences));
        Assert.Equal(Now.AddMinutes(15), jwt.ValidTo);
        Assert.Equal(UserId.ToString(), jwt.Subject);
        Assert.Equal("Ada", Claim(jwt, JwtRegisteredClaimNames.GivenName));
        Assert.Equal("Lovelace", Claim(jwt, JwtRegisteredClaimNames.FamilyName));
        Assert.Equal("ada@example.com", Claim(jwt, JwtRegisteredClaimNames.Email));
        Assert.True(Guid.TryParse(jwt.Id, out _));
        ValidateSignature(token, Secret);
    }

    [Fact]
    public void GenerateToken_AssignsANewTokenIdEachTime()
    {
        var handler = new JwtSecurityTokenHandler();
        var first = handler.ReadJwtToken(_sut.GenerateToken(UserId, "Ada", "Lovelace", "ada@example.com"));
        var second = handler.ReadJwtToken(_sut.GenerateToken(UserId, "Ada", "Lovelace", "ada@example.com"));

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void GenerateToken_RejectsASignatureFromADifferentSecret()
    {
        var token = _sut.GenerateToken(UserId, "Ada", "Lovelace", "ada@example.com");

        var exception = Record.Exception(() =>
            ValidateSignature(token, "a-different-secret-key-32-chars!!"));

        Assert.IsAssignableFrom<SecurityTokenException>(exception);
    }

    private static string Claim(JwtSecurityToken jwt, string type) =>
        Assert.Single(jwt.Claims, claim => claim.Type == type).Value;

    private static void ValidateSignature(string token, string secret)
    {
        new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "grocerease",
                ValidateAudience = true,
                ValidAudience = "grocerease-clients",
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            },
            out _);
    }
}
