using System.ComponentModel.DataAnnotations;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Listings;

namespace Faed.Web.Areas.Merchant.ViewModels;

/// <summary>
/// The merchant's listing-editing workspace: everything one listing's page needs. A record
/// so a controller action can redisplay it with one sub-form's input swapped in via
/// <c>with</c>, while every other panel keeps its normal (empty) default.
/// </summary>
public sealed record ListingWorkspacePageModel
{
    public required ListingDetailView Listing { get; init; }

    public required ListingReferenceData ReferenceData { get; init; }

    public ListingFormModel Form { get; init; } = new();

    public AddOptionModel AddOption { get; init; } = new();

    public AddOptionValueModel AddOptionValue { get; init; } = new();

    public AddVariantModel AddVariant { get; init; } = new();

    public UploadImageModel UploadImage { get; init; } = new();

    public AddEvidenceModel AddEvidence { get; init; } = new();
}

public sealed class AddOptionModel
{
    [Required(ErrorMessage = "Name the option, for example Size or Colour.")]
    [StringLength(64, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}

public sealed class AddOptionValueModel
{
    [Required]
    public Guid OptionId { get; set; }

    [Required(ErrorMessage = "Enter a value, for example M or Black.")]
    [StringLength(64, MinimumLength = 1)]
    public string Value { get; set; } = string.Empty;
}

public sealed class AddVariantModel
{
    [Required(ErrorMessage = "Enter a SKU.")]
    [StringLength(64, MinimumLength = 1)]
    public string Sku { get; set; } = string.Empty;

    [Display(Name = "Option values")]
    public List<Guid> OptionValueIds { get; set; } = [];

    [Range(0, 1_000_000)]
    [Display(Name = "Initial quantity")]
    public int InitialQuantity { get; set; }
}

public sealed class UploadImageModel
{
    [Required]
    [EnumDataType(typeof(ListingMediaType), ErrorMessage = "Choose a valid image kind.")]
    [Display(Name = "Image kind")]
    public ListingMediaType MediaType { get; set; } = ListingMediaType.Product;

    [Required(ErrorMessage = "Choose a JPG or PNG file.")]
    public IFormFile? File { get; set; }

    [StringLength(200)]
    [Display(Name = "Description (for screen readers)")]
    public string? AltText { get; set; }
}

public sealed class AddEvidenceModel
{
    [Required]
    [EnumDataType(typeof(ReferencePriceEvidenceType), ErrorMessage = "Choose a valid evidence type.")]
    [Display(Name = "Evidence type")]
    public ReferencePriceEvidenceType EvidenceType { get; set; } = ReferencePriceEvidenceType.PreviousStorePrice;

    [StringLength(2000)]
    [Display(Name = "Link")]
    [Url(ErrorMessage = "Enter a valid URL.")]
    public string? ReferenceUrl { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    [Display(Name = "Supporting file (optional)")]
    public IFormFile? File { get; set; }
}
