namespace Faed.Web.Models.Enums;

/// <summary>
/// Why a merchant changed a variant's available stock outside the normal order/deal
/// lifecycle (docs/03-BUSINESS-RULES.md §6). Stock is never silently overwritten: every
/// manual change records who, when, the delta and a reason.
/// </summary>
public enum InventoryAdjustmentType
{
    /// <summary>Additional units were found and added to the variant.</summary>
    StockFound = 0,

    /// <summary>Units were damaged or lost outside Faed and removed from the variant.</summary>
    StockLostOrDamaged = 1,

    /// <summary>A counting correction in either direction.</summary>
    ManualCorrection = 2,
}
