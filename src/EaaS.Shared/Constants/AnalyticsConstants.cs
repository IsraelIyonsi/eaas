namespace EaaS.Shared.Constants;

public static class AnalyticsConstants
{
    public static readonly TimeSpan HourlyMaxRange = TimeSpan.FromDays(7);
    public static readonly TimeSpan DailyMaxRange = TimeSpan.FromDays(90);
    public const int DefaultDateRangeDays = 30;
}
