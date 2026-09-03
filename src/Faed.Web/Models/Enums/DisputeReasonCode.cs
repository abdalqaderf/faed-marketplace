namespace Faed.Web.Models.Enums;

/// <summary>
/// Why a participant raised a dispute (docs/03-BUSINESS-RULES.md §14). A stable workflow
/// vocabulary, not admin-managed catalogue data, so it is an enum
/// (docs/19-CODING-CONVENTIONS.md "Enums vs tables"). A disclosed cosmetic issue on its own is
/// not an <see cref="UndisclosedDefect"/> claim (docs/03-BUSINESS-RULES.md §14) — the reviewing
/// administrator judges that from the evidence.
/// </summary>
public enum DisputeReasonCode
{
    /// <summary>The item's condition, description or identity did not match the listing.</summary>
    ItemNotAsDescribed = 0,

    /// <summary>A defect that was not disclosed or evidenced on the listing.</summary>
    UndisclosedDefect = 1,

    /// <summary>Declared included items, parts or accessories were missing.</summary>
    MissingItems = 2,

    /// <summary>The order or deal was never collected or delivered.</summary>
    ItemNotReceived = 3,

    /// <summary>A different product or variant was supplied.</summary>
    WrongItem = 4,

    /// <summary>Anything else; the description carries the detail.</summary>
    Other = 5,
}
