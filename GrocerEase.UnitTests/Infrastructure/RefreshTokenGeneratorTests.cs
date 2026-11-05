using System.ComponentModel.DataAnnotations;
using GrocerEase.Domain.Entities;
using GrocerEase.Infrastructure.Authentication;
using GrocerEase.UnitTests.Support;
using Microsoft.Extensions.Options;

namespace GrocerEase.UnitTests.Infrastructure;

public class RefreshTokenGeneratorTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);

    private readonly RefreshTokenGenerator _sut = new(
        Options.Create(new JwtSettings { RefreshTokenExpiryDays = 14 }),
        new FixedDateTimeProvider(Now));

    [Fact]
    public void Issue_ExpiresAfterTheConfiguredNumberOfDays()
    {
        var issued = _sut.Issue();

        Assert.Equal(Now.AddDays(14), issued.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, issued.ExpiresAt.Kind);
    }

    [Fact]
    public void Issue_ReturnsADifferentTokenEachTime()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => _sut.Issue().Token).ToHashSet();

        Assert.Equal(100, tokens.Count);
    }

    [Fact]
    public void Issue_ReturnsAUrlSafeTokenWithAtLeast256BitsOfRandomness()
    {
        var token = _sut.Issue().Token;

        // 32 random bytes in base64url is 43 characters, with no padding and none of + / =.
        Assert.Equal(43, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
    }

    [Fact]
    public void Issue_ReturnsTheHashOfTheTokenAndNotTheTokenItself()
    {
        var issued = _sut.Issue();

        Assert.Equal(_sut.Hash(issued.Token), issued.Hash);
        Assert.NotEqual(issued.Token, issued.Hash);
    }

    [Fact]
    public void Hash_IsStableAndDiffersPerToken()
    {
        Assert.Equal(_sut.Hash("abc"), _sut.Hash("abc"));
        Assert.NotEqual(_sut.Hash("abc"), _sut.Hash("abd"));
    }

    [Fact]
    public void Hash_FitsTheColumn()
    {
        Assert.True(_sut.Hash("abc").Length <= RefreshToken.TokenHashMaxLength);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Hash_RejectsAnEmptyToken(string? token)
    {
        Assert.ThrowsAny<ArgumentException>(() => _sut.Hash(token!));
    }

    [Fact]
    public void ToString_LeavesTheTokenOut()
    {
        var issued = _sut.Issue();

        Assert.DoesNotContain(issued.Token, issued.ToString());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void RefreshTokenExpiryDays_MustBeAtLeastOne(int days, bool valid)
    {
        var settings = new JwtSettings
        {
            Secret = new string('s', 32),
            Issuer = "i",
            Audience = "a",
            ExpiryMinutes = 60,
            RefreshTokenExpiryDays = days,
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(settings, new ValidationContext(settings), results, true);

        Assert.Equal(valid, isValid);
    }

    [Fact]
    public void RefreshTokenExpiryDays_DefaultsToThirty()
    {
        Assert.Equal(30, new JwtSettings().RefreshTokenExpiryDays);
    }
}
