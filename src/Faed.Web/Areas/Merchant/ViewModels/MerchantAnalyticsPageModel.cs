using Faed.Web.Services.Analytics;

namespace Faed.Web.Areas.Merchant.ViewModels;

/// <summary>Backs the merchant Analytics page.</summary>
public sealed class MerchantAnalyticsPageModel
{
    public required MerchantAnalyticsView Analytics { get; init; }
}
