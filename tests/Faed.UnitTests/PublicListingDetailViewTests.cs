using Faed.Web.Models.Enums;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Marketplace;

namespace Faed.UnitTests;

/// <summary>
/// The variant picker's server-computed disabled state
/// (<see cref="PublicListingDetailView.SellableOptionValueIds"/>), which
/// <c>wwwroot/js/listing-detail.js</c> mirrors client-side
/// (tasks/TASK-005-PUBLIC-MARKETPLACE.md "variant selection").
/// </summary>
public class PublicListingDetailViewTests
{
    private static readonly Guid Black = Guid.NewGuid();
    private static readonly Guid White = Guid.NewGuid();
    private static readonly Guid SizeM = Guid.NewGuid();
    private static readonly Guid SizeL = Guid.NewGuid();

    private static PublicListingDetailView WithVariants(params PublicListingVariantView[] variants) => new(
        Guid.NewGuid(), "Tee", "tee", "A tee.", "Clothing", "clothing", null,
        "A", "As new", "As new.", null, 19.99m, null, null,
        AllowB2C: true, AllowB2B: false, AllowMixedVariantB2B: false,
        ReturnPolicyText: null, WarrantyText: null, IncludedItemsText: null, MissingItemsText: null,
        DiscountReasonNames: [],
        Options:
        [
            new ListingOptionView(Guid.NewGuid(), "Colour",
                [new ListingOptionValueView(Black, "Black"), new ListingOptionValueView(White, "White")]),
            new ListingOptionView(Guid.NewGuid(), "Size",
                [new ListingOptionValueView(SizeM, "M"), new ListingOptionValueView(SizeL, "L")]),
        ],
        Variants: variants,
        Media: [],
        MerchantProfileId: Guid.NewGuid(), MerchantBusinessName: "Merchant", MerchantSlug: "merchant",
        MerchantIsVerified: true, PublishedAtUtc: DateTime.UtcNow);

    private static PublicListingVariantView Variant(string colour, string size, int quantity, bool active = true) =>
        new(Guid.NewGuid(), [new VariantOptionView("Colour", colour), new VariantOptionView("Size", size)], quantity, active);

    [Fact]
    public void SellableOptionValueIds_BlackMAndWhiteL_MarksEveryValueSelectable_SoTheBuyerIsNeverTrapped()
    {
        // The defect: from a valid Black/M the buyer could not reach a valid White/L, because
        // the picker disabled every White chip against the selected size M. A per-value test
        // keeps both colours and both sizes selectable; the impossible White/M pairing is left
        // for the availability line to explain.
        var view = WithVariants(Variant("Black", "M", 5), Variant("White", "L", 5));

        Assert.Equal(
            new HashSet<Guid> { Black, White, SizeM, SizeL },
            view.SellableOptionValueIds);
    }

    [Fact]
    public void SellableOptionValueIds_ExcludesAValueWhoseOnlyVariantIsDepletedOrInactive()
    {
        var view = WithVariants(
            Variant("Black", "M", 5),
            Variant("White", "L", 0),          // depleted
            Variant("Black", "L", 3, active: false)); // deactivated

        // White and L have no sellable variant left; Black and M do.
        Assert.Contains(Black, view.SellableOptionValueIds);
        Assert.Contains(SizeM, view.SellableOptionValueIds);
        Assert.DoesNotContain(White, view.SellableOptionValueIds);
        Assert.DoesNotContain(SizeL, view.SellableOptionValueIds);
    }

    [Fact]
    public void SellableOptionValueIds_WithNoOptions_IsEmpty()
    {
        var view = new PublicListingDetailView(
            Guid.NewGuid(), "Tee", "tee", "A tee.", "Clothing", "clothing", null,
            "A", "As new", "As new.", null, 19.99m, null, null,
            AllowB2C: true, AllowB2B: false, AllowMixedVariantB2B: false,
            ReturnPolicyText: null, WarrantyText: null, IncludedItemsText: null, MissingItemsText: null,
            DiscountReasonNames: [],
            Options: [],
            Variants: [new PublicListingVariantView(Guid.NewGuid(), [], 5, true)],
            Media: [],
            MerchantProfileId: Guid.NewGuid(), MerchantBusinessName: "Merchant", MerchantSlug: "merchant",
            MerchantIsVerified: true, PublishedAtUtc: DateTime.UtcNow);

        Assert.Empty(view.SellableOptionValueIds);
    }
}
