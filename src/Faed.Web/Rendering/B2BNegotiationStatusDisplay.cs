using Faed.Web.Models.Enums;
using Faed.Web.Services.B2B;

namespace Faed.Web.Rendering;

/// <summary>
/// View helper: maps B2B negotiation enums to badge classes and human labels
/// </summary>
public static class B2BNegotiationStatusDisplay
{
    public static string BadgeClass(B2BNegotiationStatus status) => status switch
    {
        B2BNegotiationStatus.Open => "faed-badge faed-badge--pending",
        B2BNegotiationStatus.Accepted => "faed-badge faed-badge--approved",
        B2BNegotiationStatus.Rejected => "faed-badge faed-badge--rejected",
        B2BNegotiationStatus.Expired => "faed-badge faed-badge--rejected",
        B2BNegotiationStatus.Cancelled => "faed-badge faed-badge--draft",
        _ => "faed-badge faed-badge--draft",
    };

    public static string Label(B2BNegotiationStatus status) => status switch
    {
        B2BNegotiationStatus.Open => "In negotiation",
        B2BNegotiationStatus.Accepted => "Accepted",
        B2BNegotiationStatus.Rejected => "Rejected",
        B2BNegotiationStatus.Expired => "Offer expired",
        B2BNegotiationStatus.Cancelled => "Cancelled",
        _ => status.ToString(),
    };

    public static string PartyLabel(B2BNegotiationParty party) => party switch
    {
        B2BNegotiationParty.SellingMerchant => "Seller",
        B2BNegotiationParty.BuyingMerchant => "Buyer",
        _ => party.ToString(),
    };
}
