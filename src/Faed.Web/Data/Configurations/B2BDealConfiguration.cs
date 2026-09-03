using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class B2BDealConfiguration : IEntityTypeConfiguration<B2BDeal>
{
    public void Configure(EntityTypeBuilder<B2BDeal> builder)
    {
        builder.ToTable("B2BDeals", table =>
            table.HasCheckConstraint(
                "CK_B2BDeals_NonNegativeMoney",
                "[AcceptedUnitPriceSnapshot] >= 0 AND [SubtotalSnapshot] >= 0 AND [TotalSnapshot] >= 0 " +
                "AND ([ShippingCostSnapshot] IS NULL OR [ShippingCostSnapshot] >= 0)"));

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        // Computed helpers on the aggregate, not persisted state.
        builder.Ignore(d => d.TotalUnits);
        builder.Ignore(d => d.HoldsReservation);
        builder.Ignore(d => d.IsTerminal);

        // Persist the workflow enums as text so deal queues and ad-hoc DB reads stay legible
        // (docs/19-CODING-CONVENTIONS.md "Enums vs tables").
        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.FulfillmentType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.ShipmentReference).HasMaxLength(B2BDeal.MaxShipmentReferenceLength);
        builder.Property(d => d.StatusReason).HasMaxLength(B2BDeal.MaxStatusReasonLength);

        // JOD is stored with three decimal places everywhere (AGENTS.md §6).
        builder.Property(d => d.AcceptedUnitPriceSnapshot).HasColumnType("decimal(18,3)");
        builder.Property(d => d.ShippingCostSnapshot).HasColumnType("decimal(18,3)");
        builder.Property(d => d.SubtotalSnapshot).HasColumnType("decimal(18,3)");
        builder.Property(d => d.TotalSnapshot).HasColumnType("decimal(18,3)");

        // Guards the two merchants acting on the same deal at the same time (AGENTS.md §7).
        builder.Property(d => d.RowVersion).IsRowVersion();

        // "Accepted negotiation creates at most one B2BDeal" (docs/17-DATA-INVARIANTS.md) —
        // enforced by the database, not only the accept use case.
        builder.HasIndex(d => d.B2BNegotiationId).IsUnique();
        builder.HasIndex(d => new { d.SellingMerchantProfileId, d.Status });
        builder.HasIndex(d => new { d.BuyingMerchantProfileId, d.Status });
        // Drives the reservation-expiry sweep (docs/06-ARCHITECTURE.md §7).
        builder.HasIndex(d => new { d.Status, d.ReservationExpiresAtUtc });

        // Transactional history is preserved, never cascade-deleted with a negotiation,
        // revision or merchant (docs/04-DOMAIN-MODEL.md §12).
        builder.HasOne<B2BNegotiation>()
            .WithMany()
            .HasForeignKey(d => d.B2BNegotiationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<B2BOfferRevision>()
            .WithMany()
            .HasForeignKey(d => d.AcceptedRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MerchantProfile>()
            .WithMany()
            .HasForeignKey(d => d.SellingMerchantProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MerchantProfile>()
            .WithMany()
            .HasForeignKey(d => d.BuyingMerchantProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(d => d.Lines).HasField("_lines");
    }
}

public sealed class B2BDealLineConfiguration : IEntityTypeConfiguration<B2BDealLine>
{
    public void Configure(EntityTypeBuilder<B2BDealLine> builder)
    {
        builder.ToTable("B2BDealLines", table =>
            table.HasCheckConstraint(
                "CK_B2BDealLines_PositiveQuantityAndMoney",
                "[Quantity] > 0 AND [UnitPriceSnapshot] >= 0 AND [LineTotalSnapshot] >= 0"));

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.UnitPriceSnapshot).HasColumnType("decimal(18,3)");
        builder.Property(l => l.LineTotalSnapshot).HasColumnType("decimal(18,3)");
        builder.Property(l => l.VariantSnapshot).IsRequired().HasMaxLength(B2BDealLine.MaxVariantSnapshotLength);

        // One line per variant on a deal.
        builder.HasIndex(l => new { l.B2BDealId, l.ListingVariantId }).IsUnique();

        builder.HasOne<B2BDeal>()
            .WithMany(d => d.Lines)
            .HasForeignKey(l => l.B2BDealId)
            .OnDelete(DeleteBehavior.Cascade);

        // The variant a deal line references must never be hard-deleted out from under the
        // deal history (docs/04-DOMAIN-MODEL.md §12).
        builder.HasOne<ListingVariant>()
            .WithMany()
            .HasForeignKey(l => l.ListingVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
