using Faed.Web.Services.Trust;

namespace Faed.Web.Areas.Admin.ViewModels;

public sealed class DisputeQueuePageModel
{
    public required DisputeQueueFilter Filter { get; init; }

    public required IReadOnlyList<DisputeSummaryView> Items { get; init; }

    public int OpenCount { get; init; }
}

public sealed class DisputeReviewPageModel
{
    public required AdminDisputeDetailView Dispute { get; init; }
}
