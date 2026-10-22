using GrocerEase.Infrastructure.Services;

namespace GrocerEase.UnitTests.Infrastructure;

public class DateTimeProviderTests
{
    [Fact]
    public void UtcNow_ReturnsTheCurrentUtcTime()
    {
        var before = DateTime.UtcNow;

        var now = new DateTimeProvider().UtcNow;

        var after = DateTime.UtcNow;
        Assert.Equal(DateTimeKind.Utc, now.Kind);
        Assert.InRange(now, before, after);
    }
}
