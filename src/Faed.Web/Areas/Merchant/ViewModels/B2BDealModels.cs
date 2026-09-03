using Faed.Web.Services.B2B;

namespace Faed.Web.Areas.Merchant.ViewModels;

public sealed class B2BDealListPageModel
{
    public required B2BDealFilter Filter { get; init; }

    public required IReadOnlyList<B2BDealSummaryView> Deals { get; init; }

    public int ActionableCount { get; init; }
}

public sealed class B2BDealDetailPageModel
{
    public required B2BDealDetailView Deal { get; init; }
}
