using GrocerEase.Application.Common.Interfaces.Services;

namespace GrocerEase.UnitTests.Support;

internal sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; } = utcNow;
}
