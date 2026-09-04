using Faed.Web.Models.Enums;

namespace Faed.Web.Rendering;

/// <summary>
/// View helper: maps B2B deal enums to badge classes and human labels
/// </summary>
public static class B2BDealStatusDisplay
{
    public static string BadgeClass(B2BDealStatus status) => status switch
    {
        B2BDealStatus.AwaitingFulfillment => "faed-badge faed-badge--pending",
        B2BDealStatus.ReadyForPickup => "faed-badge faed-badge--info",
        B2BDealStatus.Shipped => "faed-badge faed-badge--info",
        B2BDealStatus.Delivered => "faed-badge faed-badge--info",
        B2BDealStatus.Completed => "faed-badge faed-badge--approved",
        B2BDealStatus.Cancelled => "faed-badge faed-badge--rejected",
        _ => "faed-badge faed-badge--draft",
    };

    public static string Label(B2BDealStatus status) => status switch
    {
        B2BDealStatus.AwaitingFulfillment => "Awaiting fulfilment",
        B2BDealStatus.ReadyForPickup => "Ready for pickup",
        B2BDealStatus.Shipped => "Shipped",
        B2BDealStatus.Delivered => "Delivered",
        B2BDealStatus.Completed => "Completed",
        B2BDealStatus.Cancelled => "Cancelled",
        _ => status.ToString(),
    };

    public static string FulfillmentLabel(B2BFulfillmentType type) => type switch
    {
        B2BFulfillmentType.Pickup => "Direct pickup",
        B2BFulfillmentType.SellerArrangedShipping => "Seller-arranged shipping",
        _ => type.ToString(),
    };
}
