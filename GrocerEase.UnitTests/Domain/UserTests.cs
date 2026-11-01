using GrocerEase.Domain.Entities;
using GrocerEase.Domain.ValueObjects;

namespace GrocerEase.UnitTests.Domain;

public class UserTests
{
    private static readonly DateTime Created = new(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Register_BuildsAUserWithAFreshTimeOrderedId()
    {
        var user = User.Register("Ada", "Lovelace", Email.Create("ada@example.com"), "hash", Created);

        Assert.Equal(7, user.Id.Version);
        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
        Assert.Equal("ada@example.com", user.Email.Value);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(Created, user.CreatedAt);
    }

    [Fact]
    public void Register_GivesEachUserItsOwnId()
    {
        var first = User.Register("Ada", "Lovelace", Email.Create("a@example.com"), "hash", Created);
        var second = User.Register("Ada", "Lovelace", Email.Create("b@example.com"), "hash", Created);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData("", "Lovelace", "hash")]
    [InlineData("Ada", " ", "hash")]
    [InlineData("Ada", "Lovelace", "")]
    public void Register_RejectsABlankNameOrHash(string first, string last, string hash)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Register(first, last, Email.Create("ada@example.com"), hash, Created));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Register_RejectsATimeThatIsNotUtc(DateTimeKind kind)
    {
        var notUtc = new DateTime(2026, 11, 1, 9, 0, 0, kind);

        Assert.Throws<ArgumentException>(() =>
            User.Register("Ada", "Lovelace", Email.Create("ada@example.com"), "hash", notUtc));
    }

    [Fact]
    public void Register_RequiresAnEmail()
    {
        Assert.Throws<ArgumentNullException>(() => User.Register("Ada", "Lovelace", null!, "hash", Created));
    }
}
