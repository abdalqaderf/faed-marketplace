using Faed.Web.Models.Enums;

namespace Faed.Web.Services.Listings;

// ---- Inputs ------------------------------------------------------------------------

/// <summary>
/// The merchant-supplied business details of a listing. Everything authorization-sensitive
/// — the owning merchant, the listing status, stock counters — is deliberately absent: it is
/// resolved server-side and never bound from a request (docs/08-SECURITY-AND-PRIVACY.md §6).
/// </summary>
public sealed record ListingDetailsInput(
    Guid CategoryId,
    Guid? BrandId,
    Guid ConditionGradeId,
    string Title,
    string Description,
    decimal? ReferencePrice,
    decimal? RetailPrice,
    decimal? WholesaleIndicativeUnitPrice,
    int? WholesaleMinQuantity,
    bool AllowB2C,
    bool AllowB2B,
    bool AllowMixedVariantB2B,
    string? ReturnPolicyText,
    string? WarrantyText,
    string? IncludedItemsText,
    string? MissingItemsText,
    IReadOnlyList<Guid> DiscountReasonIds);

/// <summary>A sellable variant: one value per listing option, plus its opening stock.</summary>
public sealed record AddVariantInput(
    string Sku,
    IReadOnlyList<Guid> OptionValueIds,
    int InitialQuantity);

/// <summary>An image the merchant wants to attach to a listing.</summary>
public sealed record AddListingImageInput(
    ListingMediaType MediaType,
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long LengthBytes,
    string? AltText);

/// <summary>
/// Provenance for the listing's reference price: a link, a note, an uploaded document, or
/// any combination (docs/03-BUSINESS-RULES.md §4).
/// </summary>
public sealed record AddReferencePriceEvidenceInput(
    ReferencePriceEvidenceType EvidenceType,
    string? ReferenceUrl,
    string? Note,
    Stream? Content,
    string? OriginalFileName,
    string? ContentType,
    long LengthBytes);

/// <summary>A manual stock correction, always accompanied by a reason (docs/03-BUSINESS-RULES.md §6).</summary>
public sealed record StockAdjustmentInput(
    Guid VariantId,
    InventoryAdjustmentType AdjustmentType,
    int QuantityDelta,
    string Reason);

// ---- Views -------------------------------------------------------------------------

public sealed record ListingOptionValueView(Guid Id, string Value);

public sealed record ListingOptionView(Guid Id, string Name, IReadOnlyList<ListingOptionValueView> Values);

public sealed record VariantOptionView(string Option, string Value);

public sealed record ListingVariantView(
    Guid Id,
    string Sku,
    IReadOnlyList<VariantOptionView> Options,
    int AvailableQuantity,
    int ReservedQuantity,
    int SoldQuantity,
    bool IsActive)
{
    /// <summary>Human-readable combination, for example <c>Colour: Black · Size: M</c>.</summary>
    public string Combination => Options.Count == 0
        ? "Single variant"
        : string.Join(" · ", Options.Select(o => $"{o.Option}: {o.Value}"));
}

public sealed record ListingImageView(
    Guid Id,
    ListingMediaType MediaType,
    string? AltText,
    string OriginalFileName,
    long SizeBytes,
    int SortOrder);

public sealed record ListingEvidenceView(
    Guid Id,
    ReferencePriceEvidenceType EvidenceType,
    string? ReferenceUrl,
    string? Note,
    string? OriginalFileName,
    bool HasFile,
    DateTime CreatedAtUtc);

public sealed record ListingModerationView(
    Guid Id,
    ListingModerationStatus Status,
    string ReasonForReview,
    string? ReviewNote,
    DateTime SubmittedAtUtc,
    DateTime? ReviewedAtUtc);

/// <summary>A row in the merchant's own listing list.</summary>
public sealed record MerchantListingListItem(
    Guid Id,
    string Title,
    ListingStatus Status,
    string CategoryName,
    string ConditionCode,
    decimal? RetailPrice,
    int VariantCount,
    int AvailableUnits,
    bool HasDefectPhotos,
    DateTime UpdatedAtUtc,
    string? LatestReviewNote);

/// <summary>
/// Everything the merchant workspace and the admin review screen need about one listing.
/// The same projection serves both: an admin reviewing a listing must see exactly what the
/// merchant submitted.
/// </summary>
public sealed record ListingDetailView(
    Guid Id,
    Guid MerchantProfileId,
    string MerchantBusinessName,
    string Title,
    string Slug,
    string Description,
    Guid CategoryId,
    string CategoryName,
    Guid? BrandId,
    string? BrandName,
    Guid ConditionGradeId,
    string ConditionCode,
    string ConditionName,
    string ConditionDescription,
    decimal? ReferencePrice,
    decimal? RetailPrice,
    decimal? WholesaleIndicativeUnitPrice,
    int? WholesaleMinQuantity,
    bool AllowB2C,
    bool AllowB2B,
    bool AllowMixedVariantB2B,
    string? ReturnPolicyText,
    string? WarrantyText,
    string? IncludedItemsText,
    string? MissingItemsText,
    ListingStatus Status,
    bool HiddenByAdmin,
    DateTime? SubmittedAtUtc,
    DateTime? PublishedAtUtc,
    DateTime UpdatedAtUtc,
    bool AcceptsMaterialEdit,
    IReadOnlyList<string> DiscountReasonNames,
    IReadOnlyList<string> DiscountReasonCodes,
    IReadOnlyList<Guid> DiscountReasonIds,
    IReadOnlyList<ListingOptionView> Options,
    IReadOnlyList<ListingVariantView> Variants,
    IReadOnlyList<ListingImageView> Media,
    IReadOnlyList<ListingEvidenceView> ReferencePriceEvidence,
    IReadOnlyList<ListingModerationView> Moderations,
    IReadOnlyList<string> SubmissionBlockers)
{
    public int AvailableUnits => Variants.Where(v => v.IsActive).Sum(v => v.AvailableQuantity);

    public IReadOnlyList<ListingImageView> ProductPhotos =>
        [.. Media.Where(m => m.MediaType == ListingMediaType.Product).OrderBy(m => m.SortOrder)];

    public IReadOnlyList<ListingImageView> DefectPhotos =>
        [.. Media.Where(m => m.MediaType == ListingMediaType.Defect).OrderBy(m => m.SortOrder)];

    public IReadOnlyList<ListingImageView> PackagingPhotos =>
        [.. Media.Where(m => m.MediaType == ListingMediaType.Packaging).OrderBy(m => m.SortOrder)];

    /// <summary>The still-open review, when the listing is awaiting a decision.</summary>
    public ListingModerationView? PendingModeration =>
        Moderations.FirstOrDefault(m => m.Status == ListingModerationStatus.Pending);

    public ListingModerationView? LatestDecision =>
        Moderations.FirstOrDefault(m => m.Status != ListingModerationStatus.Pending);
}

/// <summary>A row in the admin listing-moderation queue.</summary>
public sealed record ModerationQueueItem(
    Guid ListingId,
    string Title,
    string MerchantBusinessName,
    ListingStatus Status,
    string ReasonForReview,
    DateTime SubmittedAtUtc,
    int VariantCount,
    int DefectPhotoCount,
    decimal? RetailPrice,
    decimal? ReferencePrice,
    bool HasReferencePriceEvidence);

/// <summary>A variant row in the merchant's inventory screen.</summary>
public sealed record InventoryRow(
    Guid VariantId,
    Guid ListingId,
    string ListingTitle,
    ListingStatus ListingStatus,
    string Sku,
    IReadOnlyList<VariantOptionView> Options,
    int AvailableQuantity,
    int ReservedQuantity,
    int SoldQuantity,
    bool IsActive,
    DateTime UpdatedAtUtc)
{
    public string Combination => Options.Count == 0
        ? "Single variant"
        : string.Join(" · ", Options.Select(o => $"{o.Option}: {o.Value}"));
}

/// <summary>
/// Counts across a merchant's <em>entire</em> inventory, independent of which page of
/// <see cref="InventoryRow"/> is on screen. "Low stock" reuses the same &le;3-unit convention
/// as the public marketplace (<c>PublicMarketplaceModels.IsLowStock</c>).
/// </summary>
public sealed record InventorySummary(
    int ActiveVariantCount,
    int LowStockVariantCount,
    int AvailableUnitsTotal,
    int ReservedUnitsTotal)
{
    public const int LowStockThreshold = 3;

    public static InventorySummary Empty { get; } = new(0, 0, 0, 0);
}

public sealed record InventoryAdjustmentView(
    Guid Id,
    Guid VariantId,
    string Sku,
    string ListingTitle,
    InventoryAdjustmentType AdjustmentType,
    int QuantityDelta,
    int QuantityBefore,
    int QuantityAfter,
    string Reason,
    DateTime CreatedAtUtc);

public sealed record CatalogChoice(Guid Id, string Label);

/// <summary>The DB-driven choices a listing form offers. Nothing here is hard-coded (TASK-003).</summary>
public sealed record ListingReferenceData(
    IReadOnlyList<CatalogChoice> Categories,
    IReadOnlyList<CatalogChoice> ConditionGrades,
    IReadOnlyList<CatalogChoice> DiscountReasons,
    IReadOnlyList<CatalogChoice> Brands);

/// <summary>Which listings a queue or list should return.</summary>
public enum MerchantListingFilter
{
    All = 0,
    Draft = 1,
    PendingReview = 2,
    Live = 3,
    Rejected = 4,
    NeedsAttention = 5,
}

public enum ModerationQueueFilter
{
    PendingReview = 0,
    Live = 1,
    Rejected = 2,
    All = 3,
}
