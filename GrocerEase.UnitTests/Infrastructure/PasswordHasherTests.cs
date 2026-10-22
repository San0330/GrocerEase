using GrocerEase.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrocerEase.UnitTests.Infrastructure;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = Hasher(4);

    [Fact]
    public void Hash_ProducesAValueThatVerifiesOnlyForTheSamePassword()
    {
        var hash = _hasher.Hash("Passw0rd!");

        Assert.NotEqual("Passw0rd!", hash);
        Assert.StartsWith("$2", hash);
        Assert.Contains("$04$", hash);
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

    [Fact]
    public void Verify_UsesTheCostStoredInTheHash()
    {
        var raised = Hasher(5);
        var hash = _hasher.Hash("Passw0rd!");

        Assert.Contains("$04$", hash);
        Assert.True(raised.Verify("Passw0rd!", hash));
        Assert.Contains("$05$", raised.Hash("Passw0rd!"));
    }

    [Fact]
    public void Cost_DefaultsToElevenWhenTheSectionIsMissing()
    {
        var settings = Bind(new Dictionary<string, string?>());

        Assert.Equal(11, settings.Cost);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(32)]
    public void Cost_OutsideTheBcryptRange_FailsOptionsValidation(int cost)
    {
        var configuration = new Dictionary<string, string?>
        {
            [$"{PasswordHashingSettings.SectionName}:Cost"] = cost.ToString(),
        };

        var exception = Assert.Throws<OptionsValidationException>(() => Bind(configuration));

        Assert.Contains(nameof(PasswordHashingSettings.Cost), exception.Message);
    }

    private static PasswordHasher Hasher(int cost) =>
        new(Options.Create(new PasswordHashingSettings { Cost = cost }));

    private static PasswordHashingSettings Bind(IEnumerable<KeyValuePair<string, string?>> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddOptions<PasswordHashingSettings>()
            .Bind(configuration.GetSection(PasswordHashingSettings.SectionName))
            .ValidateDataAnnotations();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<PasswordHashingSettings>>().Value;
    }
}
