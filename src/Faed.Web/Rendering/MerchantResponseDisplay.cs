using Faed.Web.Services.Ordering;

namespace Faed.Web.Rendering;

/// <summary>
/// View helper: turns <see cref="MerchantResponseStats"/> into the storefront line described
/// in docs/BUSINESS-MODEL.md §8.4 — "Usually responds within 2 hours · 94% response rate", or
/// a plain "New seller" badge below the five-order threshold.
/// </summary>
public static class MerchantResponseDisplay
{
    /// <summary>True when the merchant has too few orders for a rate or time to be meaningful.</summary>
    public static bool IsNewSeller(MerchantResponseStats stats) => stats.IsNewSeller;

    public static string TimeLabel(ResponseTimeBucket? bucket) => bucket switch
    {
        ResponseTimeBucket.WithinHour => "Usually responds within an hour",
        ResponseTimeBucket.WithinTwoHours => "Usually responds within 2 hours",
        ResponseTimeBucket.WithinDay => "Usually responds within a day",
        ResponseTimeBucket.MoreThanDay => "Usually responds in a few days",
        _ => "Response time not established yet",
    };

    /// <summary>The single storefront line: time bucket and rate, or "New seller".</summary>
    public static string Summary(MerchantResponseStats stats)
    {
        if (stats.IsNewSeller)
        {
            return "New seller";
        }

        return stats.ResponseRatePercent is { } percent
            ? $"{TimeLabel(stats.MedianResponse)} · {percent}% response rate"
            : "New seller";
    }
}
