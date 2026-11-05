using GrocerEase.Application.Authentication;

namespace GrocerEase.UnitTests.Application;

public class EmailFingerprintTests
{
    [Fact]
    public void Of_IsStableForTheSameAddress()
    {
        Assert.Equal(EmailFingerprint.Of("ada@example.com"), EmailFingerprint.Of("ada@example.com"));
    }

    [Theory]
    [InlineData("Ada@Example.COM")]
    [InlineData("  ada@example.com ")]
    public void Of_TreatsCaseAndSurroundingSpaceAsTheSameAccount(string variant)
    {
        Assert.Equal(EmailFingerprint.Of("ada@example.com"), EmailFingerprint.Of(variant));
    }

    [Fact]
    public void Of_DiffersBetweenAddresses()
    {
        Assert.NotEqual(EmailFingerprint.Of("ada@example.com"), EmailFingerprint.Of("grace@example.com"));
    }

    [Fact]
    public void Of_IsShortLowercaseHexThatDoesNotContainTheAddress()
    {
        var fingerprint = EmailFingerprint.Of("ada@example.com");

        Assert.Equal(16, fingerprint.Length);
        Assert.Matches("^[0-9a-f]{16}$", fingerprint);
        Assert.DoesNotContain("ada", fingerprint);
    }
}
