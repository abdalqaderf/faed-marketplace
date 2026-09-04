using Faed.Web.Models.Enums;

namespace Faed.Web.Rendering;

/// <summary>
/// View helper: maps dispute and review enums to badge classes and human labels
/// </summary>
public static class DisputeStatusDisplay
{
    public static string BadgeClass(DisputeStatus status) => status switch
    {
        DisputeStatus.Open => "faed-badge faed-badge--pending",
        DisputeStatus.UnderReview => "faed-badge faed-badge--info",
        DisputeStatus.Resolved => "faed-badge faed-badge--approved",
        DisputeStatus.Rejected => "faed-badge faed-badge--rejected",
        _ => "faed-badge faed-badge--draft",
    };

    public static string Label(DisputeStatus status) => status switch
    {
        DisputeStatus.Open => "Open",
        DisputeStatus.UnderReview => "Under review",
        DisputeStatus.Resolved => "Resolved",
        DisputeStatus.Rejected => "Dismissed",
        _ => status.ToString(),
    };

    public static string ReasonLabel(DisputeReasonCode reason) => reason switch
    {
        DisputeReasonCode.ItemNotAsDescribed => "Item not as described",
        DisputeReasonCode.UndisclosedDefect => "Undisclosed defect",
        DisputeReasonCode.MissingItems => "Missing items",
        DisputeReasonCode.ItemNotReceived => "Item not received",
        DisputeReasonCode.WrongItem => "Wrong item supplied",
        DisputeReasonCode.Other => "Other",
        _ => reason.ToString(),
    };

    public static string TransactionLabel(TrustTransactionType type) => type switch
    {
        TrustTransactionType.B2COrder => "B2C order",
        TrustTransactionType.B2BDeal => "B2B deal",
        _ => type.ToString(),
    };

    public static string Stars(int rating) => new string('★', Math.Clamp(rating, 0, 5))
        + new string('☆', 5 - Math.Clamp(rating, 0, 5));
}
