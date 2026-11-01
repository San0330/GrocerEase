using GrocerEase.Api.Errors;

namespace GrocerEase.UnitTests.Api;

public class ErrorKeysTests
{
    [Theory]
    [InlineData("Email", "email")]
    [InlineData("FirstName", "firstName")]
    [InlineData("email", "email")]
    [InlineData("Address.PostCode", "address.postCode")]
    [InlineData("Items[0].Name", "items[0].name")]
    public void ToJsonName_UsesTheNamesTheClientSent(string key, string expected)
    {
        Assert.Equal(expected, ErrorKeys.ToJsonName(key));
    }

    [Theory]
    [InlineData("$")]
    [InlineData("$.email")]
    [InlineData("$.address.postCode")]
    public void ToJsonName_LeavesJsonPathsAlone(string path)
    {
        Assert.Equal(path, ErrorKeys.ToJsonName(path));
    }
}
