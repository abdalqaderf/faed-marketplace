using Faed.Web.Services.Ordering;

namespace Faed.Web.Areas.Merchant.ViewModels;

public sealed class MerchantOrderListPageModel
{
    public required MerchantOrderFilter Filter { get; init; }

    public required IReadOnlyList<OrderSummaryView> Orders { get; init; }

    public int NeedsConfirmationCount { get; init; }
}

public sealed class MerchantOrderDetailPageModel
{
    public required OrderDetailView Order { get; init; }
}
