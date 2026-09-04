namespace Faed.Web.Models.Enums;

/// <summary>
/// Auditable admin actions. Extended as later phases add
/// account moderation.
/// </summary>
public enum AdminActionType
{
    MerchantApproved = 0,
    MerchantRejected = 1,
    MerchantSuspended = 2,
    MerchantReinstated = 3,
    MerchantVerificationDocumentAccessed = 4,
    ListingApproved = 5,
    ListingRejected = 6,
    ListingHidden = 7,
    ListingRestored = 8,

    /// <summary>An administrator started reviewing a dispute.</summary>
    DisputeReviewStarted = 9,

    /// <summary>An administrator upheld a dispute and recorded an outcome.</summary>
    DisputeResolved = 10,

    /// <summary>An administrator dismissed a dispute and recorded why.</summary>
    DisputeRejected = 11,

    /// <summary>An administrator streamed a private dispute evidence file.</summary>
    DisputeEvidenceAccessed = 12,

    /// <summary>An administrator created a catalog reference row (category / discount reason / condition grade / brand).</summary>
    CatalogItemCreated = 13,

    /// <summary>An administrator edited a catalog reference row's display fields.</summary>
    CatalogItemUpdated = 14,

    /// <summary>An administrator activated or deactivated a catalog reference row.</summary>
    CatalogItemAvailabilityChanged = 15,
}
