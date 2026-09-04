namespace Faed.Web.Models.Enums;

/// <summary>
/// How a B2C order is handed over. Faed operates no
/// warehouse or delivery fleet in the MVP: pickup happens at a
/// merchant-defined location and delivery is performed by the merchant within a
/// merchant-defined zone for a merchant-defined fee.
/// </summary>
public enum OrderFulfillmentType
{
    /// <summary>The buyer collects the order from one of the merchant's pickup locations.</summary>
    Pickup = 0,

    /// <summary>The merchant delivers the order within one of its delivery zones for a snapshotted fee.</summary>
    MerchantDelivery = 1,
}
