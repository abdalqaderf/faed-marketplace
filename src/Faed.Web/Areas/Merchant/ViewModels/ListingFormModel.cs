using System.ComponentModel.DataAnnotations;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Listings;

namespace Faed.Web.Areas.Merchant.ViewModels;

/// <summary>
/// The merchant's whole listing on one page (CORE.md §3.2): photos, title, category, the
/// condition card, warranty, price, an optional original price, quantity, and an optional
/// note. Eight of these ask the merchant to decide something; the description does not.
/// It carries no status, merchant id or SKU — those are never bound from a request.
/// </summary>
public sealed class ListingFormModel
{
    /// <summary>1 — Photos. The first product photo is the cover.</summary>
    [Display(Name = "Photos")]
    public List<IFormFile> Photos { get; set; } = [];

    /// <summary>Shown when the chosen condition card discloses a physical imperfection.</summary>
    [Display(Name = "Photo of the box or the mark")]
    public IFormFile? DefectPhoto { get; set; }

    /// <summary>Existing photos the merchant ticked for removal (edit only).</summary>
    public List<Guid> RemovePhotoIds { get; set; } = [];

    /// <summary>2 — Title.</summary>
    [Required(ErrorMessage = "Give the item a title.")]
    [StringLength(Listing.MaxTitleLength, MinimumLength = Listing.MinTitleLength,
        ErrorMessage = "The title must be between {2} and {1} characters.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>3 — Category. Shown as cards, not a dropdown.</summary>
    [Required(ErrorMessage = "Pick a category.")]
    [Display(Name = "Category")]
    public Guid? CategoryId { get; set; }

    /// <summary>4 — What's the condition? One of the four fixed cards.</summary>
    [Required(ErrorMessage = "Choose the item's condition.")]
    [Display(Name = "Condition")]
    public ConditionChoice? Condition { get; set; }

    /// <summary>Optional extra reasons from the "Add another reason" link.</summary>
    [Display(Name = "Other reasons")]
    public List<Guid> ExtraReasonIds { get; set; } = [];

    /// <summary>5 — Warranty.</summary>
    [Required]
    [EnumDataType(typeof(WarrantyType), ErrorMessage = "Choose a warranty option.")]
    [Display(Name = "Warranty")]
    public WarrantyType WarrantyType { get; set; } = WarrantyType.None;

    [Range(1, 120, ErrorMessage = "Warranty length must be between 1 and 120 months.")]
    [Display(Name = "Warranty length (months)")]
    public int? WarrantyMonths { get; set; }

    /// <summary>6 — Price.</summary>
    [Required(ErrorMessage = "Set a price.")]
    [Range(0.001, 1_000_000, ErrorMessage = "Enter a price in JOD.")]
    [Display(Name = "Price (JOD)")]
    public decimal? Price { get; set; }

    /// <summary>7 — Original price. Optional; when set it needs evidence.</summary>
    [Range(0.001, 1_000_000, ErrorMessage = "Enter the original price in JOD.")]
    [Display(Name = "Original price (JOD)")]
    public decimal? OriginalPrice { get; set; }

    [Url(ErrorMessage = "Enter a full http:// or https:// link.")]
    [StringLength(2000)]
    [Display(Name = "Link to the original price")]
    public string? OriginalPriceLink { get; set; }

    [Display(Name = "Photo of the original price")]
    public IFormFile? OriginalPriceEvidence { get; set; }

    /// <summary>8 — Quantity. Defaults to 1.</summary>
    [Range(0, 100_000, ErrorMessage = "Enter a whole number of units.")]
    [Display(Name = "Quantity")]
    public int Quantity { get; set; } = 1;

    /// <summary>The description — the ninth field, and the only one that asks for nothing.</summary>
    [StringLength(Listing.MaxDescriptionLength)]
    [Display(Name = "Anything else the buyer should know?")]
    public string? Description { get; set; }

    public static ListingFormModel ForNewListing() => new();

    public static ListingFormModel FromDetail(ListingDetailView listing)
    {
        var choice = ConditionPresets.FromStored(listing.ConditionCode, listing.DiscountReasonCodes);
        var presetReasonCodes = choice is { } c
            ? ConditionPresets.For(c).ReasonCodes
            : (IReadOnlyList<string>)[];

        // Everything the merchant selected beyond what the matched card already implies.
        // DiscountReasonIds and DiscountReasonCodes are positionally paired (see ListingQueries).
        var extraReasonIds = listing.DiscountReasonIds
            .Where((_, i) => !presetReasonCodes.Contains(listing.DiscountReasonCodes[i]))
            .ToList();

        return new ListingFormModel
        {
            Title = listing.Title,
            CategoryId = listing.CategoryId,
            Condition = choice,
            ExtraReasonIds = extraReasonIds,
            WarrantyType = listing.WarrantyType,
            WarrantyMonths = listing.WarrantyMonths,
            Price = listing.RetailPrice,
            OriginalPrice = listing.ReferencePrice,
            Quantity = listing.Variants.Count == 1 ? listing.Variants[0].AvailableQuantity : listing.AvailableUnits,
            Description = listing.Description,
        };
    }
}
