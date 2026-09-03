namespace Faed.Web.Services.B2B;

/// <summary>
/// Configurable B2B negotiation policy. Durations live in configuration, never as domain
/// constants (docs/13-OPEN-QUESTIONS.md "Important"; §14 "Default B2B offer validity" is an
/// unresolved product decision, so this is a reversible default).
/// </summary>
public sealed class B2BNegotiationOptions
{
    public const string SectionName = "B2BNegotiation";

    /// <summary>
    /// How long an offer stays valid when the proposing merchant does not choose a shorter
    /// window. Default three days (docs/13-OPEN-QUESTIONS.md §14).
    /// </summary>
    public TimeSpan DefaultOfferValidity { get; set; } = TimeSpan.FromDays(3);

    /// <summary>The shortest and longest offer validity a merchant may choose.</summary>
    public TimeSpan MinOfferValidity { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan MaxOfferValidity { get; set; } = TimeSpan.FromDays(30);

    /// <summary>How often the background sweep expires lapsed open offers. Default fifteen minutes.</summary>
    public TimeSpan ExpirySweepInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Largest quantity allowed on a single offer line.</summary>
    public int MaxOfferLineQuantity { get; set; } = 100_000;

    /// <summary>Largest number of distinct variant lines allowed on one offer.</summary>
    public int MaxOfferLines { get; set; } = 50;
}
