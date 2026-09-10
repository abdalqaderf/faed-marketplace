namespace Faed.Web.Services.Ordering;

/// <summary>Bucketed median time from a reservation to the merchant's confirmation.</summary>
public enum ResponseTimeBucket
{
    WithinHour = 0,
    WithinTwoHours = 1,
    WithinDay = 2,
    MoreThanDay = 3,
}

/// <summary>
/// A merchant's responsiveness over the rolling window. Below
/// <see cref="MinimumOrdersForStats"/> orders received, neither a rate nor a time is shown —
/// the storefront shows a neutral "New seller" badge instead, so a merchant with one
/// confirmed order cannot display "100%".
/// </summary>
public sealed record MerchantResponseStats(
    int OrdersReceived,
    int ConfirmedBeforeDeadline,
    ResponseTimeBucket? MedianResponse)
{
    public const int MinimumOrdersForStats = 5;

    public static readonly MerchantResponseStats NewSeller = new(0, 0, null);

    public bool IsNewSeller => OrdersReceived < MinimumOrdersForStats;

    /// <summary>The response rate as a whole percentage, or <c>null</c> for a new seller.</summary>
    public int? ResponseRatePercent => IsNewSeller
        ? null
        : (int)Math.Round(100.0 * ConfirmedBeforeDeadline / OrdersReceived);
}
