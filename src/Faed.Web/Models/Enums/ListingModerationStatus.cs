namespace Faed.Web.Models.Enums;

/// <summary>
/// Outcome of one admin review of one submitted listing version.
/// Rows are never rewritten, so rejection history survives.
/// </summary>
public enum ListingModerationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}
