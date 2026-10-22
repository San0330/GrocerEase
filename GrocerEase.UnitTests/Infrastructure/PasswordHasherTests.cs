using GrocerEase.Infrastructure.Authentication;

namespace GrocerEase.UnitTests.Infrastructure;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ProducesAValueThatVerifiesOnlyForTheSamePassword()
    {
        var hash = _hasher.Hash("Passw0rd!");

        Assert.NotEqual("Passw0rd!", hash);
        Assert.StartsWith("$2", hash);
        Assert.True(_hasher.Verify("Passw0rd!", hash));
        Assert.False(_hasher.Verify("different", hash));
    }

    [Fact]
    public void Hash_UsesADifferentSaltEachTime()
    {
        var first = _hasher.Hash("Passw0rd!");
        var second = _hasher.Hash("Passw0rd!");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("Passw0rd!", first));
        Assert.True(_hasher.Verify("Passw0rd!", second));
    }
}
