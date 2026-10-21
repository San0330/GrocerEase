using GrocerEase.Application.Common.Interfaces.Services;

namespace GrocerEase.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
