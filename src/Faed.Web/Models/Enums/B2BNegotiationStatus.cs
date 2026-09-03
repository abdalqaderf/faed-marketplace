namespace Faed.Web.Models.Enums;

/// <summary>
/// Merchant-to-merchant negotiation lifecycle (docs/03-BUSINESS-RULES.md §9,
/// docs/05-USER-FLOWS-AND-STATE-MACHINES.md §5). A negotiation is an offer/counter-offer
/// record, never a fulfillment record: the accepted deal, its stock reservation and its
/// own <c>ReservationExpiresAt</c> live on a separate <c>B2BDeal</c> aggregate
/// (docs/adr/0004-B2B-NEGOTIATION-SEPARATE-FROM-DEAL.md, TASK-008). Allowed transitions are
/// enforced by the <see cref="Faed.Web.Models.Entities.B2BNegotiation"/> aggregate — a
/// status is never assigned from controller input.
/// </summary>
public enum B2BNegotiationStatus
{
    /// <summary>An offer is on the table and the other merchant can accept, reject or counter it.</summary>
    Open = 0,

    /// <summary>
    /// Both merchants agreed on the current offer revision. In TASK-008 this is where the
    /// stock reservation and the <c>B2BDeal</c> are created; on its own it consumes no stock
    /// (tasks/TASK-007-B2B-NEGOTIATION.md "No stock is permanently consumed by negotiation alone").
    /// </summary>
    Accepted = 1,

    /// <summary>The merchant on the receiving end of the current offer turned it down. Terminal.</summary>
    Rejected = 2,

    /// <summary>The current offer revision's <c>OfferExpiresAtUtc</c> passed while the negotiation was still open. Terminal.</summary>
    Expired = 3,

    /// <summary>A participating merchant withdrew from the negotiation before it was accepted. Terminal.</summary>
    Cancelled = 4,
}
