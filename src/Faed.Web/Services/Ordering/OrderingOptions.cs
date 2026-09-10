namespace Faed.Web.Services.Ordering;

/// <summary>
/// Configurable B2C ordering policy. The three windows are the deadlines behind
/// "nothing waits on a human forever" (docs/BUSINESS-MODEL.md §8.2): every state that
/// depends on a person acting resolves itself by the time the window elapses.
/// </summary>
public sealed class OrderingOptions
{
    public const string SectionName = "Ordering";

    /// <summary>
    /// How long a placed but unconfirmed reservation holds its stock before the sweep cancels
    /// it and tells the buyer the shop did not respond. Twelve hours, not six: a reservation
    /// placed at 11pm with a six-hour window would expire at 5am with the shop shut, killing a
    /// good order. Twelve hours covers the late-night reservation and still resolves within
    /// half a day.
    /// </summary>
    public TimeSpan ReservationWindow { get; set; } = TimeSpan.FromHours(12);

    /// <summary>
    /// How long a confirmed order may sit without the merchant preparing it for handover
    /// before the sweep records a no-show and returns the stock to sale.
    /// </summary>
    public TimeSpan NoShowWindow { get; set; } = TimeSpan.FromHours(48);

    /// <summary>
    /// How long an order marked ready for pickup (or out for delivery) may sit with no further
    /// movement before the sweep closes it as a no-show and releases the stock.
    /// </summary>
    public TimeSpan AutoCloseWindow { get; set; } = TimeSpan.FromHours(72);

    /// <summary>How often the background sweep looks for orders past a deadline. Default five minutes.</summary>
    public TimeSpan ExpirySweepInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Largest quantity of a single variant a buyer may put on one order line.</summary>
    public int MaxUnitsPerLine { get; set; } = 50;
}
