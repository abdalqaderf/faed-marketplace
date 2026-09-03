namespace Faed.Web.Models.Enums;

/// <summary>
/// Which kind of transaction a dispute or review points at. A dispute and a review each
/// reference exactly one transaction context — a B2C order or a B2B deal
/// (docs/03-BUSINESS-RULES.md §13-14, docs/17-DATA-INVARIANTS.md "Dispute" / "Review"). Used
/// to discriminate request input; the stored entity keeps a nullable FK per kind and a
/// database check constraint that exactly one is set.
/// </summary>
public enum TrustTransactionType
{
    /// <summary>A B2C <see cref="Faed.Web.Models.Entities.Order"/>.</summary>
    B2COrder = 0,

    /// <summary>A B2B <see cref="Faed.Web.Models.Entities.B2BDeal"/>.</summary>
    B2BDeal = 1,
}
