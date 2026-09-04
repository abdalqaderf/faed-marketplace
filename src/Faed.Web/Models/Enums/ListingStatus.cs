namespace Faed.Web.Models.Enums;

/// <summary>
/// Listing lifecycle.
/// Only <see cref="Live"/> is publicly visible. A material edit to a published listing
/// returns it to <see cref="PendingReview"/> rather than staying public
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
