namespace Faed.Web.Services.Listings;

/// <summary>
/// Configurable listing and upload limits (docs/06-ARCHITECTURE.md §11). The launch B2B
/// minimum of 10 units is a policy default, not a platform constant
/// (docs/03-BUSINESS-RULES.md §11), so it lives here rather than in the domain.
/// </summary>
public sealed class ListingOptions
{
    public const string SectionName = "Listings";

    /// <summary>Launch default and platform floor for a listing's B2B minimum order quantity.</summary>
    public int DefaultB2BMinimumQuantity { get; set; } = 10;

    /// <summary>Maximum accepted size for a single listing image or evidence file.</summary>
    public long MaxImageBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>Maximum images of any one kind (product, defect, packaging) per listing.</summary>
    public int MaxImagesPerType { get; set; } = 12;

    /// <summary>Maximum reference-price evidence records per listing.</summary>
    public int MaxReferencePriceEvidencePerListing { get; set; } = 6;

    /// <summary>Maximum options (for example Size and Colour) a listing may vary along.</summary>
    public int MaxOptionsPerListing { get; set; } = 3;

    /// <summary>Maximum values a single option may offer.</summary>
    public int MaxValuesPerOption { get; set; } = 30;

    /// <summary>Maximum sellable variants a single listing may hold.</summary>
    public int MaxVariantsPerListing { get; set; } = 200;
}
