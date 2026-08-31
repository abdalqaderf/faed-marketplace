using System.ComponentModel.DataAnnotations;

namespace Faed.Web.Areas.Merchant.ViewModels;

/// <summary>Input model for creating or editing merchant business details.</summary>
public sealed class MerchantApplicationFormModel
{
    [Required]
    [Display(Name = "Business name")]
    [StringLength(200, MinimumLength = 2)]
    public string BusinessName { get; set; } = string.Empty;

    [Display(Name = "Contact email")]
    [EmailAddress]
    [StringLength(256)]
    public string? ContactEmail { get; set; }

    [Display(Name = "Contact phone")]
    [Phone]
    [StringLength(32)]
    public string? ContactPhone { get; set; }
}
