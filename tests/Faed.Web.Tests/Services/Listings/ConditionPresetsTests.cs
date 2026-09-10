using Faed.Web.Services.Listings;
using Xunit;

namespace Faed.Web.Tests.Services.Listings;

/// <summary>
/// PHASE-PLAN.md Phase 8: the condition-card → grade + reason mapping lives in one class, and
/// one click on a card must fill both internal fields (CORE.md §3.3).
/// </summary>
public class ConditionPresetsTests
{
    [Theory]
    [InlineData(ConditionChoice.Sealed, "A", "Overstock")]
    [InlineData(ConditionChoice.BoxOpenedOrDamaged, "B", "PackagingDamage")]
    [InlineData(ConditionChoice.CustomerReturn, "C", "CustomerReturn")]
    [InlineData(ConditionChoice.ExDisplay, "D", "DisplayItem")]
    public void For_MapsEachCardToItsGradeAndReason(ConditionChoice choice, string gradeCode, string reasonCode)
    {
        var preset = ConditionPresets.For(choice);

        Assert.Equal(gradeCode, preset.GradeCode);
        Assert.Equal(new[] { reasonCode }, preset.ReasonCodes);
    }

    [Theory]
    [InlineData(ConditionChoice.Sealed, false)]
    [InlineData(ConditionChoice.BoxOpenedOrDamaged, true)]
    [InlineData(ConditionChoice.CustomerReturn, false)]
    [InlineData(ConditionChoice.ExDisplay, true)]
    public void For_RevealsDefectPhotoOnlyForCards2And4(ConditionChoice choice, bool reveals)
    {
        Assert.Equal(reveals, ConditionPresets.For(choice).RevealsDefectPhoto);
    }

    [Fact]
    public void All_CoversEveryChoiceExactlyOnce()
    {
        Assert.Equal(Enum.GetValues<ConditionChoice>().Length, ConditionPresets.All.Count);
        Assert.Equal(ConditionPresets.All.Count, ConditionPresets.All.Select(p => p.Choice).Distinct().Count());
    }

    [Fact]
    public void FromStored_RoundTripsACardsOwnCodes()
    {
        var preset = ConditionPresets.For(ConditionChoice.ExDisplay);

        var recovered = ConditionPresets.FromStored(preset.GradeCode, preset.ReasonCodes.ToArray());

        Assert.Equal(ConditionChoice.ExDisplay, recovered);
    }

    [Fact]
    public void FromStored_IgnoresExtraAdvancedReasons()
    {
        // A merchant who used "Add another reason" still matches the card they picked.
        var recovered = ConditionPresets.FromStored("A", ["Overstock", "SupersededModel"]);

        Assert.Equal(ConditionChoice.Sealed, recovered);
    }

    [Fact]
    public void FromStored_ReturnsNullWhenNoCardMatches()
    {
        Assert.Null(ConditionPresets.FromStored("A", ["SupersededModel"]));
    }
}
