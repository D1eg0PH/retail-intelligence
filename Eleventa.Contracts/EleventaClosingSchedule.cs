namespace RetailIntelligence.Contracts.Eleventa;

public static class EleventaClosingSchedule
{
    public static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
    public static readonly TimeOnly ClosingTime = new(22, 35);

    public static bool IsWeekly(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday;
    public static bool IsMonthly(DateOnly date) => date.Day == DateTime.DaysInMonth(date.Year, date.Month);
    public static bool IsClosingDay(DateOnly date) => IsWeekly(date) || IsMonthly(date);
    public static DateTime LocalTime(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime;
    public static DateTimeOffset ScheduledAt(DateOnly date) =>
        new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(ClosingTime, DateTimeKind.Unspecified), TimeZone), TimeSpan.Zero);

    // Never reconstruct a missed day from inventory captured the following day.
    public static bool IsCaptureDue(DateTimeOffset now, DateTimeOffset? lastCaptured)
    {
        var date = DateOnly.FromDateTime(LocalTime(now));
        if (!IsClosingDay(date) || now < ScheduledAt(date)) return false;
        return lastCaptured is null || DateOnly.FromDateTime(LocalTime(lastCaptured.Value)) != date;
    }

    public static TimeSpan NextDelay(DateTimeOffset now, TimeSpan regularDelay)
    {
        var date = DateOnly.FromDateTime(LocalTime(now));
        var remaining = ScheduledAt(date) - now;
        return IsClosingDay(date) && remaining > TimeSpan.Zero && remaining < regularDelay ? remaining : regularDelay;
    }
}
