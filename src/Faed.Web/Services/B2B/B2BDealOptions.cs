namespace Faed.Web.Services.B2B;

/// <summary>
/// Configurable accepted-deal policy. Durations live in configuration, never as domain
/// constants (docs/13-OPEN-QUESTIONS.md "Important"; §15 "Default accepted-deal reservation
/// duration" is an unresolved product decision, so this is a reversible default).
/// </summary>
public sealed class B2BDealOptions
{
    public const string SectionName = "B2BDeal";

    /// <summary>
    /// How long a deal holds its stock reservation before the selling merchant starts
    /// fulfilling it. Wholesale fulfilment (arranging a pickup slot or a carrier) needs more
    /// time than a B2C reservation, so the default is seven days
    /// (docs/13-OPEN-QUESTIONS.md §15).
    /// </summary>
    public TimeSpan ReservationWindow { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How often the background sweep releases lapsed deal reservations. Default fifteen minutes.</summary>
    public TimeSpan ExpirySweepInterval { get; set; } = TimeSpan.FromMinutes(15);
}
