using GrocerEase.Domain.ValueObjects;

namespace GrocerEase.UnitTests.Domain;

public class EmailTests
{
    [Fact]
    public void Create_StoresTheAddressInLowerCase()
    {
        Assert.Equal("ada@example.com", Email.Create("Ada@Example.COM").Value);
    }

    [Fact]
    public void TwoAddressesThatDifferOnlyInCase_AreEqual()
    {
        Assert.Equal(Email.Create("Ada@Example.com"), Email.Create("ada@EXAMPLE.com"));
        Assert.Equal(Email.Create("Ada@Example.com").GetHashCode(), Email.Create("ada@EXAMPLE.com").GetHashCode());
    }

    [Fact]
    public void DifferentAddresses_AreNotEqual()
    {
        Assert.NotEqual(Email.Create("ada@example.com"), Email.Create("grace@example.com"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsABlankAddress(string address)
    {
        Assert.Throws<ArgumentException>(() => Email.Create(address));
    }

    [Fact]
    public void Create_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Email.Create(null!));
    }

    [Fact]
    public void ToString_IsTheAddress()
    {
        Assert.Equal("ada@example.com", Email.Create("Ada@Example.com").ToString());
    }
}
