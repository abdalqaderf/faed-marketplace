namespace Faed.Web.Models.Enums;

/// <summary>
/// How an accepted B2B deal is handed over (docs/03-BUSINESS-RULES.md §12). Faed operates no
/// warehouse or delivery fleet in the MVP (AGENTS.md §3) and does not book or price shipping:
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
