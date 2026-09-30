namespace BevosTacos.Services;

//The truck is in Austin, so dates and times are shown in Central time
public static class AustinTime
{
    private static readonly TimeZoneInfo Central = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Central);

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(ToLocal(clock.GetUtcNow().UtcDateTime));

    public static string Format(DateTime utc) => ToLocal(utc).ToString("MMM d, h:mm tt");
}
