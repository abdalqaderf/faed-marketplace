using Faed.Application.Merchants;

namespace Faed.Web.Areas.Admin.Models;

/// <summary>Display model for the admin merchant-verification queue.</summary>
public sealed class MerchantQueuePageModel
{
    public required MerchantQueueFilter Filter { get; init; }

    public required IReadOnlyList<MerchantQueueItem> Items { get; init; }

    public required int PendingCount { get; init; }
}
