using Faed.Web.Models.Enums;

namespace Faed.Web.Rendering;

/// <summary>View helper: maps listing workflow enums to badge classes and human labels
///.</summary>
public static class ListingStatusDisplay
{
    public static string BadgeClass(ListingStatus status) => status switch
    {
        ListingStatus.Draft => "faed-badge faed-badge--draft",
        ListingStatus.PendingReview => "faed-badge faed-badge--pending",
        ListingStatus.Live => "faed-badge faed-badge--approved",
        ListingStatus.Rejected => "faed-badge faed-badge--rejected",
        ListingStatus.Hidden => "faed-badge faed-badge--draft",
        ListingStatus.SoldOut => "faed-badge faed-badge--info",
        ListingStatus.Archived => "faed-badge faed-badge--draft",
        _ => "faed-badge faed-badge--draft",
    };

    public static string Label(ListingStatus status) => status switch
    {
        ListingStatus.Draft => "Draft",
        ListingStatus.PendingReview => "Pending review",
        ListingStatus.Live => "Live",
        ListingStatus.Rejected => "Rejected",
        ListingStatus.Hidden => "Hidden",
        ListingStatus.SoldOut => "Sold out",
        ListingStatus.Archived => "Archived",
        _ => status.ToString(),
    };

    public static string MediaTypeLabel(ListingMediaType type) => type switch
    {
        ListingMediaType.Product => "Product photo",
        ListingMediaType.Defect => "Defect photo",
        ListingMediaType.Packaging => "Packaging photo",
        _ => type.ToString(),
    };

    public static string EvidenceTypeLabel(ReferencePriceEvidenceType type) => type switch
    {
        ReferencePriceEvidenceType.Photo => "Photo",
        ReferencePriceEvidenceType.Link => "Link",
        _ => type.ToString(),
    };

    public static string WarrantyTypeLabel(WarrantyType type) => type switch
    {
        WarrantyType.None => "None",
        WarrantyType.ManufacturerWarranty => "Manufacturer warranty",
        WarrantyType.ShopWarranty => "Shop warranty",
        _ => type.ToString(),
    };

    public static string ModerationStatusLabel(ListingModerationStatus status) => status switch
    {
        ListingModerationStatus.Pending => "Pending",
        ListingModerationStatus.Approved => "Approved",
        ListingModerationStatus.Rejected => "Rejected",
        _ => status.ToString(),
    };
}
