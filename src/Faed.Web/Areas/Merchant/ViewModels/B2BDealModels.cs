using Faed.Web.Services.B2B;
using Faed.Web.Services.Common;
using Faed.Web.Services.Trust;

namespace Faed.Web.Areas.Merchant.ViewModels;

public sealed class B2BDealListPageModel
{
    public required B2BDealFilter Filter { get; init; }

    public required PagedResult<B2BDealSummaryView> Deals { get; init; }

    public int ActionableCount { get; init; }
}

public sealed class B2BDealDetailPageModel
{
    public required B2BDealDetailView Deal { get; init; }

    /// <summary>Whether the buying merchant may review the seller for this deal, and any review already left.</summary>
    public ReviewEligibilityView? ReviewEligibility { get; init; }

    /// <summary>An active (Open/UnderReview) dispute on this deal that the caller is party to.</summary>
    public DisputeSummaryView? ActiveDispute { get; init; }

    /// <summary>Closed disputes on this deal, shown as history.</summary>
    public IReadOnlyList<DisputeSummaryView> PastDisputes { get; init; } = [];

    public MerchantLeaveReviewFormModel ReviewForm { get; set; } = new();

    public bool CanRaiseDispute { get; init; }
}
