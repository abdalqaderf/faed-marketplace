namespace Faed.Web.Models.Enums;

/// <summary>
/// The fulfillment lifecycle of an accepted merchant-to-merchant deal
/// (docs/03-BUSINESS-RULES.md §10, docs/05-USER-FLOWS-AND-STATE-MACHINES.md §7). A deal is a
/// fulfillment record and is distinct from the <see cref="B2BNegotiationStatus"/> that
/// preceded it (AGENTS.md Rule C, docs/adr/0004-B2B-NEGOTIATION-SEPARATE-FROM-DEAL.md):
/// the deal carries its own <c>ReservationExpiresAtUtc</c>, separate from a revision's
/// <c>OfferExpiresAtUtc</c>. Allowed transitions are enforced by the
/// <see cref="Faed.Web.Models.Entities.B2BDeal"/> aggregate — a status is never assigned
/// from controller input.
///
/// The dispute path (<c>Disputed</c>, docs/03-BUSINESS-RULES.md §10) is introduced with the
/// trust phase (TASK-009) and is deliberately absent here.
/// </summary>
public enum B2BDealStatus
{
    /// <summary>Stock is reserved and the selling merchant has not started fulfilling the deal.</summary>
    AwaitingFulfillment = 0,

    /// <summary>A direct-pickup deal the seller has prepared for the buying merchant to collect.</summary>
    ReadyForPickup = 1,

    /// <summary>A seller-arranged-shipping deal the seller has dispatched.</summary>
    Shipped = 2,

    /// <summary>The buying merchant has taken delivery; awaiting final confirmation.</summary>
    Delivered = 3,

    /// <summary>Fulfilment finished; reserved stock became sold stock. Terminal.</summary>
    Completed = 4,

    /// <summary>Cancelled by a participant or the reservation-expiry sweep before completion; reserved stock was released. Terminal.</summary>
    Cancelled = 5,
}
