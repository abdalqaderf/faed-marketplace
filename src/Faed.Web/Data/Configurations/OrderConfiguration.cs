using Faed.Web.Models.Entities;
using Faed.Web.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table =>
            table.HasCheckConstraint(
                "CK_Orders_NonNegativeMoney",
                "[Subtotal] >= 0 AND [Total] >= 0 AND [DeliveryFeeSnapshot] >= 0"));

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        // Computed helpers on the aggregate, not persisted state.
        builder.Ignore(o => o.HoldsReservation);
        builder.Ignore(o => o.IsTerminal);
        builder.Ignore(o => o.BuyerCanCancel);
        builder.Ignore(o => o.MerchantCanCancel);
        builder.Ignore(o => o.TotalUnits);

        builder.Property(o => o.BuyerUserId).IsRequired().HasMaxLength(450);

        builder.Property(o => o.ContactName).IsRequired().HasMaxLength(Order.MaxContactNameLength);
        builder.Property(o => o.ContactPhone).IsRequired().HasMaxLength(Order.MaxContactPhoneLength);
        builder.Property(o => o.DeliveryAddressText).HasMaxLength(Order.MaxDeliveryAddressLength);
        builder.Property(o => o.BuyerNote).HasMaxLength(Order.MaxBuyerNoteLength);
        builder.Property(o => o.StatusReason).HasMaxLength(Order.MaxStatusReasonLength);
        builder.Property(o => o.FulfillmentSnapshot).IsRequired().HasMaxLength(Order.MaxFulfillmentSnapshotLength);

        // JOD is stored with three decimal places everywhere.
        builder.Property(o => o.Subtotal).HasColumnType("decimal(18,3)");
        builder.Property(o => o.Total).HasColumnType("decimal(18,3)");
        builder.Property(o => o.DeliveryFeeSnapshot).HasColumnType("decimal(18,3)");

        // Persist the workflow enums as text so order queues and ad-hoc DB reads stay legible
        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(o => o.FulfillmentType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Guards a buyer cancellation racing a merchant status transition on the same order.
        builder.Property(o => o.RowVersion).IsRowVersion();

        builder.HasIndex(o => new { o.BuyerUserId, o.CreatedAtUtc });
        builder.HasIndex(o => new { o.MerchantProfileId, o.Status });
        // Drives the reservation-expiry sweep.
        builder.HasIndex(o => new { o.Status, o.ReservationExpiresAtUtc });

        // "Order has exactly one Buyer" is enforced referentially
        // against the Identity user, and — like the MerchantProfile → ApplicationUser
        // relationship — the delete behaviour is Restrict so a buyer with order history can
        // never be hard-deleted out from under it.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(o => o.BuyerUserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);

        // Transactional history is preserved, never cascade-deleted with a merchant, location
        // or zone.
        builder.HasOne<MerchantProfile>()
            .WithMany()
            .HasForeignKey(o => o.MerchantProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MerchantLocation>()
            .WithMany()
            .HasForeignKey(o => o.MerchantLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MerchantDeliveryZone>()
            .WithMany()
            .HasForeignKey(o => o.DeliveryZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(o => o.Items).HasField("_items");
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", table =>
            table.HasCheckConstraint(
                "CK_OrderItems_PositiveQuantityAndMoney",
                "[Quantity] > 0 AND [UnitPriceSnapshot] >= 0 AND [LineTotalSnapshot] >= 0"));

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.UnitPriceSnapshot).HasColumnType("decimal(18,3)");
        builder.Property(i => i.LineTotalSnapshot).HasColumnType("decimal(18,3)");

        builder.Property(i => i.ListingTitleSnapshot).IsRequired().HasMaxLength(OrderItem.MaxTitleSnapshotLength);
        builder.Property(i => i.VariantSnapshot).IsRequired().HasMaxLength(OrderItem.MaxVariantSnapshotLength);
        builder.Property(i => i.ConditionGradeSnapshot).IsRequired().HasMaxLength(OrderItem.MaxConditionSnapshotLength);
        builder.Property(i => i.DiscountReasonSnapshot).HasMaxLength(OrderItem.MaxDiscountReasonSnapshotLength);

        // One line per variant on an order.
        builder.HasIndex(i => new { i.OrderId, i.ListingVariantId }).IsUnique();

        builder.HasOne<Order>()
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // The listing/variant an order line references must never be hard-deleted out from
        // under the order history.
        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(i => i.ListingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ListingVariant>()
            .WithMany()
            .HasForeignKey(i => i.ListingVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
