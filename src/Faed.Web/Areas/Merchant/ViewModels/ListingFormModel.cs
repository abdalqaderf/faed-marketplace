using System.ComponentModel.DataAnnotations;
using Faed.Web.Models.Entities;
using Faed.Web.Services.Listings;

namespace Faed.Web.Areas.Merchant.ViewModels;

/// <summary>
/// Input model for a listing's business details. Deliberately carries no status, merchant id
/// or stock field — those are never bound from a request
/// (docs/08-SECURITY-AND-PRIVACY.md §6).
/// </summary>
public sealed class ListingFormModel
{
    [Required(ErrorMessage = "Choose a category.")]
    [Display(Name = "Category")]
    public Guid? CategoryId { get; set; }

    [Display(Name = "Brand")]
    public Guid? BrandId { get; set; }

    [Required(ErrorMessage = "Choose a condition grade.")]
    [Display(Name = "Condition grade")]
    public Guid? ConditionGradeId { get; set; }

    [Required]
    [StringLength(Listing.MaxTitleLength, MinimumLength = Listing.MinTitleLength)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(Listing.MaxDescriptionLength, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Discount reasons")]
    public List<Guid> DiscountReasonIds { get; set; } = [];

    [Display(Name = "Reference price (JOD)")]
    [Range(0, 1_000_000)]
    public decimal? ReferencePrice { get; set; }

    [Display(Name = "Retail price (JOD)")]
    [Range(0, 1_000_000)]
    public decimal? RetailPrice { get; set; }

    [Display(Name = "Wholesale indicative price (JOD)")]
    [Range(0, 1_000_000)]
    public decimal? WholesaleIndicativeUnitPrice { get; set; }

    [Display(Name = "B2B minimum order quantity")]
    [Range(1, 1_000_000)]
    public int? WholesaleMinQuantity { get; set; }

    [Display(Name = "Sell to individual buyers (B2C)")]
    public bool AllowB2C { get; set; } = true;

    [Display(Name = "Sell to other merchants (B2B)")]
    public bool AllowB2B { get; set; }

    [Display(Name = "Allow mixed variants toward the B2B minimum")]
    public bool AllowMixedVariantB2B { get; set; }

    [StringLength(Listing.MaxPolicyTextLength)]
    [Display(Name = "Return policy")]
    public string? ReturnPolicyText { get; set; }

    [StringLength(Listing.MaxPolicyTextLength)]
    [Display(Name = "Warranty")]
    public string? WarrantyText { get; set; }

    [StringLength(Listing.MaxPolicyTextLength)]
    [Display(Name = "What's included")]
    public string? IncludedItemsText { get; set; }

    [StringLength(Listing.MaxPolicyTextLength)]
    [Display(Name = "What's missing")]
    public string? MissingItemsText { get; set; }

    public ListingDetailsInput ToInput() => new(
        CategoryId!.Value,
        BrandId,
        ConditionGradeId!.Value,
        Title,
        Description,
        ReferencePrice,
        RetailPrice,
        WholesaleIndicativeUnitPrice,
        WholesaleMinQuantity,
        AllowB2C,
        AllowB2B,
        AllowMixedVariantB2B,
        ReturnPolicyText,
        WarrantyText,
        IncludedItemsText,
        MissingItemsText,
        DiscountReasonIds);

    public static ListingFormModel FromDetail(ListingDetailView listing) => new()
    {
        CategoryId = listing.CategoryId,
        BrandId = listing.BrandId,
        ConditionGradeId = listing.ConditionGradeId,
        Title = listing.Title,
        Description = listing.Description,
        DiscountReasonIds = [.. listing.DiscountReasonIds],
        ReferencePrice = listing.ReferencePrice,
        RetailPrice = listing.RetailPrice,
        WholesaleIndicativeUnitPrice = listing.WholesaleIndicativeUnitPrice,
        WholesaleMinQuantity = listing.WholesaleMinQuantity,
        AllowB2C = listing.AllowB2C,
        AllowB2B = listing.AllowB2B,
        AllowMixedVariantB2B = listing.AllowMixedVariantB2B,
        ReturnPolicyText = listing.ReturnPolicyText,
        WarrantyText = listing.WarrantyText,
        IncludedItemsText = listing.IncludedItemsText,
        MissingItemsText = listing.MissingItemsText,
    };
}
