using Faed.Web.Models.Enums;

namespace Faed.Web.Rendering;

/// <summary>View helper: maps a verification status to its badge class and human label
/// (docs/07-UI-UX-SPEC.md §8 — never show a bare code without meaning).</summary>
public static class MerchantStatusDisplay
{
    public static string BadgeClass(MerchantVerificationStatus status) => status switch
    {
        MerchantVerificationStatus.Draft => "faed-badge faed-badge--draft",
        MerchantVerificationStatus.PendingReview => "faed-badge faed-badge--pending",
        MerchantVerificationStatus.Approved => "faed-badge faed-badge--approved",
        MerchantVerificationStatus.Rejected => "faed-badge faed-badge--rejected",
        MerchantVerificationStatus.Suspended => "faed-badge faed-badge--suspended",
        _ => "faed-badge faed-badge--draft",
    };

    public static string Label(MerchantVerificationStatus status) => status switch
    {
        MerchantVerificationStatus.Draft => "Draft",
        MerchantVerificationStatus.PendingReview => "Pending review",
        MerchantVerificationStatus.Approved => "Approved",
        MerchantVerificationStatus.Rejected => "Rejected",
        MerchantVerificationStatus.Suspended => "Suspended",
        _ => status.ToString(),
    };

    public static string DocumentTypeLabel(MerchantVerificationDocumentType type) => type switch
    {
        MerchantVerificationDocumentType.CommercialRegistration => "Commercial registration",
        MerchantVerificationDocumentType.TaxRegistration => "Tax registration",
        MerchantVerificationDocumentType.Other => "Other supporting document",
        _ => type.ToString(),
    };
}
