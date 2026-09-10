namespace Faed.Web.Services.Listings;

/// <summary>
/// The four cards the merchant actually sees for "What's the condition?" (CORE.md §3.3).
/// The names are deliberately plain — no grade letters, no "discount reason".
/// </summary>
public enum ConditionChoice
{
    Sealed = 0,
    BoxOpenedOrDamaged = 1,
    CustomerReturn = 2,
    ExDisplay = 3,
}

/// <summary>
/// One condition card: the merchant-facing text, and the two internal taxonomies it fills in
/// — a <see cref="Models.Entities.ConditionGrade"/> code and one or more
/// <see cref="Models.Entities.DiscountReason"/> codes. Cards 2 and 4 also reveal the
/// defect-photo upload, because their own wording discloses a physical imperfection.
/// </summary>
public sealed record ConditionPreset(
    ConditionChoice Choice,
    string Heading,
    string Blurb,
    string GradeCode,
    IReadOnlyList<string> ReasonCodes,
    bool RevealsDefectPhoto);

/// <summary>
/// The single place the condition-card → grade + reason mapping lives. One click on a card
/// sets <c>ConditionGradeId</c> and <c>DiscountReasonIds</c> together; the view never spells
/// the mapping out and the merchant never learns the internal vocabulary.
/// </summary>
public static class ConditionPresets
{
    public static readonly IReadOnlyList<ConditionPreset> All =
    [
        new(ConditionChoice.Sealed,
            "Sealed",
            "New, in an unopened box.",
            "A", ["Overstock"], RevealsDefectPhoto: false),

        new(ConditionChoice.BoxOpenedOrDamaged,
            "Box opened or damaged",
            "The item itself is new and unused.",
            "B", ["PackagingDamage"], RevealsDefectPhoto: true),

        new(ConditionChoice.CustomerReturn,
            "Customer return",
            "Opened and checked, but never used.",
            "C", ["CustomerReturn"], RevealsDefectPhoto: false),

        new(ConditionChoice.ExDisplay,
            "Ex-display",
            "Light scratches or marks from showroom use.",
            "D", ["DisplayItem"], RevealsDefectPhoto: true),
    ];

    public static ConditionPreset For(ConditionChoice choice) =>
        All.SingleOrDefault(p => p.Choice == choice)
        ?? throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unknown condition choice.");

    /// <summary>
    /// Best-effort reverse lookup for the edit screen: given a listing's stored grade code and
    /// discount-reason codes, which card was chosen. Returns <c>null</c> when the listing was
    /// built through the advanced path and matches no single card, so the caller can fall back
    /// to showing the reasons individually.
    /// </summary>
    public static ConditionChoice? FromStored(string gradeCode, IReadOnlyCollection<string> reasonCodes) =>
        All.FirstOrDefault(p =>
                string.Equals(p.GradeCode, gradeCode, StringComparison.OrdinalIgnoreCase)
                && p.ReasonCodes.All(reasonCodes.Contains))
            ?.Choice;
}
