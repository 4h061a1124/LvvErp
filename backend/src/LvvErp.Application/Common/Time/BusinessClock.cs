namespace LvvErp.Application.Common.Time;

/// <summary>"Today" as the factory sees it (Vietnam time), regardless of the server's time zone.</summary>
public sealed class BusinessClock(TimeProvider timeProvider)
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Zone).DateTime);
}
