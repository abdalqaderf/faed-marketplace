using Faed.Application.Merchants;

namespace Faed.Web.Areas.Merchant.Models;

/// <summary>Display model for the merchant verification overview page.</summary>
public sealed class MerchantVerificationPageModel
{
    public required MerchantApplicationView? Application { get; init; }

    public VerificationDocumentUploadModel Upload { get; init; } = new();

    public bool HasApplication => Application is not null;
}
