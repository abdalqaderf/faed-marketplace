using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;

namespace Faed.Web.Areas.Admin.ViewModels;

public sealed class ListingModerationQueuePageModel
{
    public required ModerationQueueFilter Filter { get; init; }

    public required PagedResult<ModerationQueueItem> Items { get; init; }

    public required int PendingCount { get; init; }
}

public sealed class ListingModerationDetailPageModel
{
    public required ListingDetailView Listing { get; init; }
}
