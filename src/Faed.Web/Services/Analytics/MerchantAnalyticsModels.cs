using System.Globalization;

namespace Faed.Web.Services.Analytics;

/// <summary>
/// One stale-inventory row: a published listing that has sat past the configured threshold
/// without selling a single unit (docs/01-PRD.md §14 "stale listings").
/// </summary>
public sealed record StaleListingView(
    Guid ListingId,
    string Title,
    string Slug,
    DateTime PublishedAtUtc,
    int AvailableUnits,
    int AgeDays);

/// <summary>
/// A merchant's recovery analytics, every figure derived from server-side transaction and
/// listing data — never from a merchant-editable total (docs/03-BUSINESS-RULES.md §15,
/// docs/08-SECURITY-AND-PRIVACY.md §6). All money is <c>JOD</c> with three decimals.
/// </summary>
public sealed record MerchantAnalyticsView(
    decimal RecoveredValueB2C,
    decimal RecoveredValueB2B,
    int UnitsListed,
    int UnitsSoldB2C,
    int UnitsSoldB2B,
    int CompletedOrders,
    int CompletedDeals,
    double? AverageDaysToSale,
    int CancelledOrders,
    int NoShowOrders,
    int CancelledDeals,
    int ActiveNegotiations,
    IReadOnlyList<StaleListingView> StaleListings,
    TimeSpan StaleListingThreshold)
{
    public decimal RecoveredValueTotal => RecoveredValueB2C + RecoveredValueB2B;

    public int UnitsSoldTotal => UnitsSoldB2C + UnitsSoldB2B;

    /// <summary>Units sold ÷ units listed, as a fraction in [0, 1]. Zero when nothing is listed.</summary>
    public double SellThroughRate => UnitsListed == 0 ? 0d : (double)UnitsSoldTotal / UnitsListed;

    public bool HasAnyActivity =>
        UnitsListed > 0 || CompletedOrders > 0 || CompletedDeals > 0
        || CancelledOrders > 0 || CancelledDeals > 0 || ActiveNegotiations > 0;

    /// <summary>The exact configured duration in concise, user-facing English.</summary>
    public string StaleListingThresholdLabel => FormatDuration(StaleListingThreshold);

    private static string FormatDuration(TimeSpan duration)
    {
        var parts = new List<string>(4);
        AddPart(parts, duration.Days, "day");
        AddPart(parts, duration.Hours, "hour");
        AddPart(parts, duration.Minutes, "minute");

        var seconds = duration.Seconds
            + (duration.Ticks % TimeSpan.TicksPerSecond) / (double)TimeSpan.TicksPerSecond;
        if (seconds > 0 || parts.Count == 0)
        {
            var value = seconds.ToString("0.#######", CultureInfo.InvariantCulture);
            parts.Add($"{value} second{(seconds == 1d ? string.Empty : "s")}");
        }

        return string.Join(" ", parts);
    }

    private static void AddPart(List<string> parts, int value, string unit)
    {
        if (value > 0)
        {
            parts.Add($"{value} {unit}{(value == 1 ? string.Empty : "s")}");
        }
    }
}
