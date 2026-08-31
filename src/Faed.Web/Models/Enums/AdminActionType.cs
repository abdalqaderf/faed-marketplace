namespace Faed.Web.Models.Enums;

/// <summary>
/// Auditable admin actions (docs/04-DOMAIN-MODEL.md §10). Extended as later phases add
/// listing moderation, dispute resolution and account moderation.
/// </summary>
public enum AdminActionType
{
    MerchantApproved = 0,
    MerchantRejected = 1,
    MerchantSuspended = 2,
    MerchantReinstated = 3,
    MerchantVerificationDocumentAccessed = 4,
}
