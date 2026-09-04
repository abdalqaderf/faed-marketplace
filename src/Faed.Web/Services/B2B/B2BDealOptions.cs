namespace Faed.Web.Services.B2B;

/// <summary>
/// Configurable accepted-deal policy. Durations live in configuration, never as domain
/// constants.
/// </summary>
public sealed class B2BDealOptions
{
    public const string SectionName = "B2BDeal";

    /// <summary>
    /// How long a deal holds its stock reservation before the selling merchant starts
    /// fulfilling it. Wholesale fulfilment (arranging a pickup slot or a carrier) needs more
    /// time than a B2C reservation, so the default is seven days
    /// </summary>
    public TimeSpan ReservationWindow { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How often the background sweep releases lapsed deal reservations. Default fifteen minutes.</summary>
    public TimeSpan ExpirySweepInterval { get; set; } = TimeSpan.FromMinutes(15);
}
