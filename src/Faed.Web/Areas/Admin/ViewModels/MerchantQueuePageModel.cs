using Faed.Web.Services.Common;
using Faed.Web.Services.Merchants;

namespace Faed.Web.Areas.Admin.ViewModels;

/// <summary>Display model for the admin merchant-verification queue.</summary>
public sealed class MerchantQueuePageModel
{
    public required MerchantQueueFilter Filter { get; init; }

    public required PagedResult<MerchantQueueItem> Items { get; init; }

    public required int PendingCount { get; init; }
}
