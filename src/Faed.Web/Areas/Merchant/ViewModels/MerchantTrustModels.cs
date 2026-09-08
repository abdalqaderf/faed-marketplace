using Faed.Web.Services.Trust;

namespace Faed.Web.Areas.Merchant.ViewModels;

public sealed class MerchantReviewsPageModel
{
    public required MerchantReviewHistoryView Reviews { get; init; }
}
