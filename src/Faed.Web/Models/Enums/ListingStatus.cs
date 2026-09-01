namespace Faed.Web.Models.Enums;

/// <summary>
/// Listing lifecycle (docs/03-BUSINESS-RULES.md §2, docs/05-USER-FLOWS-AND-STATE-MACHINES.md §2).
/// Only <see cref="Live"/> is publicly visible. A material edit to a published listing
/// returns it to <see cref="PendingReview"/> rather than staying public
/// (docs/02-SCOPE-AND-DECISIONS.md "Listing moderation policy", AGENTS.md §8).
/// </summary>
public enum ListingStatus
{
    Draft = 0,
    PendingReview = 1,
    Live = 2,
    Rejected = 3,
    Hidden = 4,
    SoldOut = 5,
    Archived = 6,
}
