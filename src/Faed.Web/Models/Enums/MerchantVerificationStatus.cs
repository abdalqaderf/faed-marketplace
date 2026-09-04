namespace Faed.Web.Models.Enums;

/// <summary>
/// Merchant verification lifecycle.
/// This is a domain state, not an Identity role.
/// </summary>
public enum MerchantVerificationStatus
{
    Draft = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3,
    Suspended = 4,
}
