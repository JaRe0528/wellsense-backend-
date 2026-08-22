using WellSense.Application.Common.Interfaces;

namespace WellSense.Infrastructure.Common;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
