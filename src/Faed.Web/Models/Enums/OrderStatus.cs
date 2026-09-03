namespace Faed.Web.Models.Enums;

/// <summary>
/// B2C order lifecycle (docs/03-BUSINESS-RULES.md §8, docs/05-USER-FLOWS-AND-STATE-MACHINES.md §4).
/// Allowed transitions are enforced explicitly by the <see cref="Faed.Web.Models.Entities.Order"/>
/// aggregate — a status is never assigned from controller input (docs/03-BUSINESS-RULES.md §8).
/// The dispute path (<c>Disputed</c>) is introduced with the trust phase (TASK-009) and is
/// deliberately absent here.
/// </summary>
public enum OrderStatus
{
    /// <summary>Stock is reserved and the order is waiting for the merchant to confirm it.</summary>
    Pending = 0,

    /// <summary>The merchant accepted the order; stock stays reserved until fulfillment or cancellation.</summary>
    Confirmed = 1,

    /// <summary>A pickup order the merchant has prepared for the buyer to collect.</summary>
    ReadyForPickup = 2,

    /// <summary>A delivery order the merchant has dispatched.</summary>
    OutForDelivery = 3,

    /// <summary>Fulfilment finished; reserved stock became sold stock. Terminal.</summary>
    Completed = 4,

    /// <summary>Cancelled by the buyer, the merchant, or the reservation-expiry sweep; reserved stock was released. Terminal.</summary>
    Cancelled = 5,

    /// <summary>The buyer did not collect / receive the order; reserved stock was released. Terminal.</summary>
    NoShow = 6,
}
