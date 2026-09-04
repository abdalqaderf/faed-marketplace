namespace Faed.Web.Services.Ordering;

/// <summary>
/// Configurable B2C ordering policy.
/// </summary>
public sealed class OrderingOptions
{
    public const string SectionName = "Ordering";

    /// <summary>
    /// How long a placed but unconfirmed order holds its stock reservation before the
    /// expiry sweep releases it. Default one hour.
    /// </summary>
    public TimeSpan ReservationWindow { get; set; } = TimeSpan.FromHours(1);

    /// <summary>How often the background sweep looks for expired reservations. Default five minutes.</summary>
    public TimeSpan ExpirySweepInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Largest quantity of a single variant a buyer may put on one order line.</summary>
    public int MaxUnitsPerLine { get; set; } = 50;
}
