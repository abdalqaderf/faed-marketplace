namespace Faed.Web.Models.Enums;

/// <summary>
/// The lifecycle of a post-transaction dispute. A dispute is a record that hangs off exactly one completed
/// or in-fulfilment transaction — a B2C <see cref="Faed.Web.Models.Entities.Order"/> or a B2B
/// <see cref="Faed.Web.Models.Entities.B2BDeal"/>. It never mutates that transaction's own
/// status or its stock: the order/deal state machines are unchanged by this phase.
/// Allowed transitions are enforced by the <see cref="Faed.Web.Models.Entities.Dispute"/>
/// aggregate — a status is never assigned from controller input.
/// </summary>
public enum DisputeStatus
{
    /// <summary>Filed by a participant and waiting for an administrator to pick it up.</summary>
    Open = 0,

    /// <summary>An administrator is actively reviewing the dispute and its evidence.</summary>
    UnderReview = 1,

    /// <summary>An administrator upheld the dispute and recorded an outcome. Terminal.</summary>
    Resolved = 2,

    /// <summary>An administrator dismissed the dispute and recorded why. Terminal.</summary>
    Rejected = 3,
}
