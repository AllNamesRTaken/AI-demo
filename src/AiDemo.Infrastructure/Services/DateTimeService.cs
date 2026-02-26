namespace AiDemo.Infrastructure.Services;

public interface IDateTimeService
{
    DateTime UtcNow { get; }
}

public sealed class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}
