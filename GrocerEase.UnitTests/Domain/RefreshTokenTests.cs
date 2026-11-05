using GrocerEase.Domain.Entities;

namespace GrocerEase.UnitTests.Domain;

public class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Expiry = Now.AddDays(30);

    private static RefreshToken NewToken() => RefreshToken.StartFamily(Guid.NewGuid(), "hash-1", Now, Expiry);

    [Fact]
    public void StartFamily_BuildsAnActiveTokenInANewFamily()
    {
        var userId = Guid.NewGuid();

        var token = RefreshToken.StartFamily(userId, "hash-1", Now, Expiry);

        Assert.Equal(7, token.Id.Version);
        Assert.Equal(7, token.FamilyId.Version);
        Assert.Equal(userId, token.UserId);
        Assert.Equal("hash-1", token.TokenHash);
        Assert.Equal(Now, token.CreatedAt);
        Assert.Equal(Expiry, token.ExpiresAt);
        Assert.Null(token.RevokedAt);
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public void StartFamily_GivesEachLoginItsOwnFamily()
    {
        Assert.NotEqual(NewToken().FamilyId, NewToken().FamilyId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void StartFamily_RejectsABlankHash(string hash)
    {
        Assert.Throws<ArgumentException>(() => RefreshToken.StartFamily(Guid.NewGuid(), hash, Now, Expiry));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void StartFamily_RejectsATimeThatIsNotUtc(DateTimeKind kind)
    {
        var notUtc = new DateTime(2026, 11, 5, 9, 0, 0, kind);

        Assert.Throws<ArgumentException>(() => RefreshToken.StartFamily(Guid.NewGuid(), "hash", notUtc, Expiry));
        Assert.Throws<ArgumentException>(() => RefreshToken.StartFamily(Guid.NewGuid(), "hash", Now, notUtc));
    }

    [Fact]
    public void StartFamily_RejectsAnExpiryThatIsNotAfterTheCreationTime()
    {
        Assert.Throws<ArgumentException>(() => RefreshToken.StartFamily(Guid.NewGuid(), "hash", Now, Now));
    }

    [Fact]
    public void IsExpired_IsFalseUntilTheExpiryInstant()
    {
        var token = NewToken();

        Assert.False(token.IsExpired(Expiry.AddTicks(-1)));
        Assert.True(token.IsExpired(Expiry));
    }

    [Fact]
    public void Revoke_RecordsWhenItHappened()
    {
        var token = NewToken();

        token.Revoke(Now.AddMinutes(5));

        Assert.True(token.IsRevoked);
        Assert.Equal(Now.AddMinutes(5), token.RevokedAt);
    }

    [Fact]
    public void Revoke_KeepsTheFirstTimeWhenCalledAgain()
    {
        var token = NewToken();
        token.Revoke(Now.AddMinutes(5));

        token.Revoke(Now.AddMinutes(10));

        Assert.Equal(Now.AddMinutes(5), token.RevokedAt);
    }

    [Fact]
    public void Revoke_RejectsATimeThatIsNotUtc()
    {
        Assert.Throws<ArgumentException>(() =>
            NewToken().Revoke(new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Local)));
    }

    [Fact]
    public void Rotate_RevokesThisTokenAndReturnsASuccessorInTheSameFamily()
    {
        var token = NewToken();
        var later = Now.AddMinutes(30);

        var next = token.Rotate("hash-2", later, later.AddDays(30));

        Assert.True(token.IsRevoked);
        Assert.Equal(later, token.RevokedAt);
        Assert.Equal(token.FamilyId, next.FamilyId);
        Assert.Equal(token.UserId, next.UserId);
        Assert.NotEqual(token.Id, next.Id);
        Assert.Equal("hash-2", next.TokenHash);
        Assert.Equal(later, next.CreatedAt);
        Assert.Equal(later.AddDays(30), next.ExpiresAt);
        Assert.False(next.IsRevoked);
    }

    [Fact]
    public void Rotate_RefusesARevokedToken()
    {
        var token = NewToken();
        token.Revoke(Now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() => token.Rotate("hash-2", Now.AddMinutes(2), Expiry));
    }

    [Fact]
    public void Rotate_RefusesAnExpiredToken()
    {
        var token = NewToken();

        Assert.Throws<InvalidOperationException>(() => token.Rotate("hash-2", Expiry, Expiry.AddDays(30)));
    }
}
