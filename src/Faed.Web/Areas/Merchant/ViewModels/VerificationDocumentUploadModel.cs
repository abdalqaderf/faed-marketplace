using System.ComponentModel.DataAnnotations;
using Faed.Web.Models.Enums;

namespace Faed.Web.Areas.Merchant.ViewModels;

/// <summary>Input model for attaching one verification document.</summary>
public sealed class VerificationDocumentUploadModel
{
    [Required]
    [EnumDataType(typeof(MerchantVerificationDocumentType), ErrorMessage = "Choose a valid document type.")]
    [Display(Name = "Document type")]
    public MerchantVerificationDocumentType DocumentType { get; set; } = MerchantVerificationDocumentType.CommercialRegistration;

    [Required(ErrorMessage = "Choose a PDF, JPG or PNG file.")]
    [Display(Name = "File")]
    public IFormFile? File { get; set; }
}
