using System.ComponentModel.DataAnnotations;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Listings;

namespace Faed.Web.Areas.Merchant.ViewModels;

/// <summary>
/// Input model for a listing's business details. Deliberately carries no status, merchant id
/// or stock field — those are never bound from a request
/// </summary>
public sealed class ListingFormModel
{
    [Required(ErrorMessage = "Choose a category.")]
    [Display(Name = "Category")]
    public Guid? CategoryId { get; set; }

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

    [StringLength(Listing.MaxPolicyTextLength)]
    [Display(Name = "Return policy")]
    public string? ReturnPolicyText { get; set; }

    [Required]
    [EnumDataType(typeof(WarrantyType), ErrorMessage = "Choose a valid warranty option.")]
    [Display(Name = "Warranty")]
    public WarrantyType WarrantyType { get; set; } = WarrantyType.None;

    [Range(1, 120, ErrorMessage = "Warranty length must be between 1 and 120 months.")]
    [Display(Name = "Warranty length (months)")]
    public int? WarrantyMonths { get; set; }

    [StringLength(Listing.MaxPolicyTextLength)]
    [Display(Name = "What's included")]
    public string? IncludedItemsText { get; set; }

    [StringLength(Listing.MaxPolicyTextLength)]
    [Display(Name = "What's missing")]
    public string? MissingItemsText { get; set; }

    public ListingDetailsInput ToInput() => new(
        CategoryId!.Value,
        ConditionGradeId!.Value,
        Title,
        Description,
        ReferencePrice,
        RetailPrice,
        ReturnPolicyText,
        WarrantyType,
        WarrantyMonths,
        IncludedItemsText,
        MissingItemsText,
        DiscountReasonIds);

    public static ListingFormModel FromDetail(ListingDetailView listing) => new()
    {
        CategoryId = listing.CategoryId,
        ConditionGradeId = listing.ConditionGradeId,
        Title = listing.Title,
        Description = listing.Description,
        DiscountReasonIds = [.. listing.DiscountReasonIds],
        ReferencePrice = listing.ReferencePrice,
        RetailPrice = listing.RetailPrice,
        ReturnPolicyText = listing.ReturnPolicyText,
        WarrantyType = listing.WarrantyType,
        WarrantyMonths = listing.WarrantyMonths,
        IncludedItemsText = listing.IncludedItemsText,
        MissingItemsText = listing.MissingItemsText,
    };
}
