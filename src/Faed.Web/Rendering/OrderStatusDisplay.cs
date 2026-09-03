using Faed.Web.Models.Enums;

namespace Faed.Web.Rendering;

/// <summary>
/// View helper: maps B2C order enums to badge classes and human labels
/// (docs/07-UI-UX-SPEC.md §11 — never communicate state through colour alone; §2 — plain
/// commerce wording).
/// </summary>
public static class OrderStatusDisplay
{
    public static string BadgeClass(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "faed-badge faed-badge--pending",
        OrderStatus.Confirmed => "faed-badge faed-badge--info",
        OrderStatus.ReadyForPickup => "faed-badge faed-badge--info",
        OrderStatus.OutForDelivery => "faed-badge faed-badge--info",
        OrderStatus.Completed => "faed-badge faed-badge--approved",
        OrderStatus.Cancelled => "faed-badge faed-badge--rejected",
        OrderStatus.NoShow => "faed-badge faed-badge--rejected",
        _ => "faed-badge faed-badge--draft",
    };

    public static string Label(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Awaiting merchant confirmation",
        OrderStatus.Confirmed => "Confirmed",
        OrderStatus.ReadyForPickup => "Ready for pickup",
        OrderStatus.OutForDelivery => "Out for delivery",
        OrderStatus.Completed => "Completed",
        OrderStatus.Cancelled => "Cancelled",
        OrderStatus.NoShow => "No-show",
        _ => status.ToString(),
    };

    public static string FulfillmentLabel(OrderFulfillmentType type) => type switch
    {
        OrderFulfillmentType.Pickup => "Pickup",
        OrderFulfillmentType.MerchantDelivery => "Merchant delivery",
        _ => type.ToString(),
    };
}
