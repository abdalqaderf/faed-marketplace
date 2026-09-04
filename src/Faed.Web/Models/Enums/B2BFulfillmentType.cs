namespace Faed.Web.Models.Enums;

/// <summary>
/// How an accepted B2B deal is handed over. Faed operates no
/// warehouse or delivery fleet in the MVP and does not book or price shipping:
/// the parties either arrange a direct pickup or the seller arranges shipping and records a
/// reference for it.
/// </summary>
public enum B2BFulfillmentType
{
    /// <summary>The buying merchant collects the lot directly from the selling merchant.</summary>
    Pickup = 0,

    /// <summary>The selling merchant arranges shipping; Faed only stores a seller-entered shipment reference.</summary>
    SellerArrangedShipping = 1,
}
