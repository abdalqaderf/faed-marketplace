using Faed.Web.Services.Common;
using Faed.Web.Services.Subscriptions;

namespace Faed.Web.Areas.Admin.ViewModels;

/// <summary>Display model for the admin subscriptions queue.</summary>
public sealed class SubscriptionQueuePageModel
{
    public required SubscriptionQueueFilter Filter { get; init; }

    public required PagedResult<SubscriptionQueueItem> Items { get; init; }
}

/// <summary>The payment-reference form shared by Activate and Extend.</summary>
public sealed class SubscriptionPaymentFormModel
{
    public string PaymentReference { get; set; } = string.Empty;
}
