using Faed.Web.Services.Analytics;

namespace Faed.Web.Areas.Merchant.ViewModels;

/// <summary>Backs the merchant Analytics page (docs/07-UI-UX-SPEC.md §6).</summary>
public sealed class MerchantAnalyticsPageModel
{
    public required MerchantAnalyticsView Analytics { get; init; }
}
