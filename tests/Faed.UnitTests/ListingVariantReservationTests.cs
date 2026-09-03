using Faed.Web.Models;
using Faed.Web.Models.Entities;

namespace Faed.UnitTests;

/// <summary>
/// Variant-level reservation accounting used by the B2C order flow
/// (tasks/TASK-006-B2C-ORDERS.md, docs/03-BUSINESS-RULES.md §7,
/// docs/17-DATA-INVARIANTS.md "Inventory"). The <c>rowversion</c> race itself is proven
/// against real SQL Server in the integration suite.
/// </summary>
public class ListingVariantReservationTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    private static ListingVariant Variant(int initialQuantity)
    {
        var listing = new Listing(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Sneakers", "sneakers", "Comfortable.", Now);
        var option = listing.AddOption("Size", Now);
        var value = listing.AddOptionValue(option.Id, "M", Now);
        return listing.AddVariant("SNK-M", [value.Id], initialQuantity, Now);
    }

    [Fact]
    public void Reserve_MovesAvailableToReserved()
    {
        var variant = Variant(5);

        variant.Reserve(2, Now);

        Assert.Equal(3, variant.AvailableQuantity);
        Assert.Equal(2, variant.ReservedQuantity);
        Assert.Equal(0, variant.SoldQuantity);
    }

    [Fact]
    public void Reserve_MoreThanAvailable_IsRejected_AndLeavesStockUntouched()
    {
        var variant = Variant(1);

        Assert.Throws<DomainException>(() => variant.Reserve(2, Now));
        Assert.Equal(1, variant.AvailableQuantity);
        Assert.Equal(0, variant.ReservedQuantity);
    }

    [Fact]
    public void Reserve_OnAnInactiveVariant_IsRejected()
    {
        var variant = Variant(5);
        variant.Deactivate(Now);

        Assert.Throws<DomainException>(() => variant.Reserve(1, Now));
    }

    [Fact]
    public void ReleaseReservation_ReturnsReservedUnitsToAvailable()
    {
        var variant = Variant(5);
        variant.Reserve(3, Now);

        variant.ReleaseReservation(3, Now);

        Assert.Equal(5, variant.AvailableQuantity);
        Assert.Equal(0, variant.ReservedQuantity);
    }

    [Fact]
    public void ConfirmSale_MovesReservedToSold_NotBackToAvailable()
    {
        var variant = Variant(5);
        variant.Reserve(2, Now);

        variant.ConfirmSale(2, Now);

        Assert.Equal(3, variant.AvailableQuantity);
        Assert.Equal(0, variant.ReservedQuantity);
        Assert.Equal(2, variant.SoldQuantity);
    }

    [Fact]
    public void ReleaseOrConfirm_MoreThanReserved_IsRejected()
    {
        var variant = Variant(5);
        variant.Reserve(1, Now);

        Assert.Throws<DomainException>(() => variant.ReleaseReservation(2, Now));
        Assert.Throws<DomainException>(() => variant.ConfirmSale(2, Now));
    }

    [Fact]
    public void ReservationLifecycle_PreservesTheStockAccountingInvariant()
    {
        var variant = Variant(10);

        variant.Reserve(4, Now);
        variant.ReleaseReservation(1, Now);
        variant.ConfirmSale(3, Now);

        // Initial == Available + Reserved + Sold (docs/03-BUSINESS-RULES.md §5).
        Assert.Equal(
            variant.InitialQuantity,
            variant.AvailableQuantity + variant.ReservedQuantity + variant.SoldQuantity);
        Assert.Equal(7, variant.AvailableQuantity);
        Assert.Equal(0, variant.ReservedQuantity);
        Assert.Equal(3, variant.SoldQuantity);
    }
}
