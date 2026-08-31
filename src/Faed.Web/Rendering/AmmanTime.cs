namespace Faed.Web.Rendering;

/// <summary>
/// View helper for displaying stored UTC timestamps in Jordan local time. Timestamps are
/// stored in UTC and displayed for <c>Asia/Amman</c> (AGENTS.md §3 "English UI",
/// docs/02-SCOPE-AND-DECISIONS.md).
/// </summary>
public static class AmmanTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>Date and time, e.g. <c>31 Aug 2026 15:00</c>.</summary>
    public static string FormatDateTime(DateTime utc) => $"{ToLocal(utc):d MMM yyyy HH:mm}";

    /// <summary>Date only, e.g. <c>31 Aug 2026</c>.</summary>
    public static string FormatDate(DateTime utc) => $"{ToLocal(utc):d MMM yyyy}";

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Asia/Amman", "Jordan Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
