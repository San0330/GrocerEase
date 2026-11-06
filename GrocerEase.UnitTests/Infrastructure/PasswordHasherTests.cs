using System.Globalization;
using GrocerEase.Infrastructure.Authentication;
using GrocerEase.UnitTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrocerEase.UnitTests.Infrastructure;

public sealed class PasswordHasherTests : IDisposable
{
    private readonly TestMetrics _metrics = new();
    private readonly PasswordHasher _hasher;

    public PasswordHasherTests() => _hasher = Hasher(4);

    public void Dispose() => _metrics.Dispose();

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
    public void Verify_WithNoStoredHash_ReturnsFalseForAnyPassword()
    {
        Assert.False(_hasher.Verify("Passw0rd!", null));
        Assert.False(_hasher.Verify("", null));
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
            [$"{PasswordHashingSettings.SectionName}:Cost"] = cost.ToString(CultureInfo.InvariantCulture),
        };

        var exception = Assert.Throws<OptionsValidationException>(() => Bind(configuration));

        Assert.Contains(nameof(PasswordHashingSettings.Cost), exception.Message);
    }

    [Fact]
    public void Hash_AndVerify_RecordHowLongEachTook()
    {
        var duration = _metrics.Measure("grocerease.password.hash.duration");

        var hash = _hasher.Hash("Passw0rd!");
        _hasher.Verify("Passw0rd!", hash);

        var measurements = duration.GetMeasurementSnapshot();
        Assert.Equal(["hash", "verify"], measurements.Select(m => m.Tags["operation"]));
        Assert.All(measurements, m => Assert.True(m.Value > 0, "a BCrypt call takes measurable time"));
    }

    [Fact]
    public void Verify_WithNoStoredHash_StillRecordsTheStandInCheck_SoTheTimingCannotRevealAMissingUser()
    {
        var duration = _metrics.Measure("grocerease.password.hash.duration");

        _hasher.Verify("Passw0rd!", null);

        Assert.Equal("verify", Assert.Single(duration.GetMeasurementSnapshot()).Tags["operation"]);
    }

    [Fact]
    public void TheDurationTagsNeverHoldThePassword()
    {
        var duration = _metrics.Measure("grocerease.password.hash.duration");

        _hasher.Hash("Passw0rd!");

        var tags = Assert.Single(duration.GetMeasurementSnapshot()).Tags;
        Assert.DoesNotContain(tags.Values, value => value?.ToString()?.Contains("Passw0rd!") == true);
        Assert.Equal(["operation"], tags.Keys);
    }

    private PasswordHasher Hasher(int cost) =>
        new(Options.Create(new PasswordHashingSettings { Cost = cost }), _metrics.Metrics);

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
