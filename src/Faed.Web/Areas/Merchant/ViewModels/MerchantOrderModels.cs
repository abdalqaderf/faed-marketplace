using Faed.Web.Services.Ordering;
using Faed.Web.Services.Trust;

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

    /// <summary>An active (Open/UnderReview) dispute on this order, if there is one.</summary>
    public DisputeSummaryView? ActiveDispute { get; init; }

    /// <summary>Closed disputes on this order, shown as history.</summary>
    public IReadOnlyList<DisputeSummaryView> PastDisputes { get; init; } = [];

    /// <summary>True when the selling merchant may open a new dispute for this order.</summary>
    public bool CanRaiseDispute { get; init; }
}
