namespace Faed.Web.Models.Enums;

/// <summary>
/// Merchant verification lifecycle (docs/03-BUSINESS-RULES.md §1, docs/05-USER-FLOWS-AND-STATE-MACHINES.md §1).
/// This is a domain state, not an Identity role (docs/08-SECURITY-AND-PRIVACY.md §1).
/// </summary>
public enum MerchantVerificationStatus
{
    Draft = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3,
    Suspended = 4,
}
