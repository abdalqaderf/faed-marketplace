using Faed.Web.Models.Enums;

namespace Faed.Web.Rendering;

/// <summary>
/// View helper: plain-English labels for audited admin actions in the audit-log viewer
/// </summary>
public static class AdminActivityDisplay
{
    public static string Label(AdminActionType actionType) => actionType switch
    {
        AdminActionType.MerchantApproved => "Merchant approved",
        AdminActionType.MerchantRejected => "Merchant rejected",
        AdminActionType.MerchantSuspended => "Merchant suspended",
        AdminActionType.MerchantReinstated => "Merchant reinstated",
        AdminActionType.MerchantVerificationDocumentAccessed => "Verification document opened",
        AdminActionType.ListingApproved => "Listing approved",
        AdminActionType.ListingRejected => "Listing rejected",
        AdminActionType.ListingHidden => "Listing hidden",
        AdminActionType.ListingRestored => "Listing restored",
        AdminActionType.DisputeReviewStarted => "Dispute review started",
        AdminActionType.DisputeResolved => "Dispute resolved",
        AdminActionType.DisputeRejected => "Dispute dismissed",
        AdminActionType.DisputeEvidenceAccessed => "Dispute evidence opened",
        AdminActionType.CatalogItemCreated => "Catalog item created",
        AdminActionType.CatalogItemUpdated => "Catalog item edited",
        AdminActionType.CatalogItemAvailabilityChanged => "Catalog item availability changed",
        _ => actionType.ToString(),
    };
}
