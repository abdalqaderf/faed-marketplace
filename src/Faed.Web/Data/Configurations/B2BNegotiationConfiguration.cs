using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class B2BNegotiationConfiguration : IEntityTypeConfiguration<B2BNegotiation>
{
    public void Configure(EntityTypeBuilder<B2BNegotiation> builder)
    {
        builder.ToTable("B2BNegotiations");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        // Computed helpers on the aggregate, not persisted state.
        builder.Ignore(n => n.CurrentRevision);
        builder.Ignore(n => n.IsOpen);
        builder.Ignore(n => n.AwaitingResponseFrom);

        builder.Property(n => n.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Guards a buyer and a seller acting on the same negotiation at the same time.
        builder.Property(n => n.RowVersion).IsRowVersion();

        // Supports the merchant negotiation queues.
        builder.HasIndex(n => new { n.SellingMerchantProfileId, n.Status });
        builder.HasIndex(n => new { n.BuyingMerchantProfileId, n.Status });
        builder.HasIndex(n => n.ListingId);

        // Transactional history is preserved, never cascade-deleted with a listing or a
        // merchant. Two merchant foreign keys from one row rule
        // out cascade anyway.
        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(n => n.ListingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MerchantProfile>()
            .WithMany()
            .HasForeignKey(n => n.SellingMerchantProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MerchantProfile>()
            .WithMany()
            .HasForeignKey(n => n.BuyingMerchantProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(n => n.Revisions).HasField("_revisions");
    }
}

public sealed class B2BOfferRevisionConfiguration : IEntityTypeConfiguration<B2BOfferRevision>
{
    public void Configure(EntityTypeBuilder<B2BOfferRevision> builder)
    {
        builder.ToTable("B2BOfferRevisions", table =>
            table.HasCheckConstraint(
                "CK_B2BOfferRevisions_NonNegativeMoney",
                "[ProposedUnitPrice] >= 0 AND [ProposedTotal] >= 0"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Ignore(r => r.TotalQuantity);

        // JOD is stored with three decimal places everywhere.
        builder.Property(r => r.ProposedUnitPrice).HasColumnType("decimal(18,3)");
        builder.Property(r => r.ProposedTotal).HasColumnType("decimal(18,3)");

        builder.Property(r => r.Message).HasMaxLength(B2BOfferRevision.MaxMessageLength);

        // "Revision numbers are strictly increasing and unique per negotiation"
        // — enforced by the database, not only the aggregate.
        builder.HasIndex(r => new { r.B2BNegotiationId, r.RevisionNumber }).IsUnique();

        builder.HasOne<B2BNegotiation>()
            .WithMany(n => n.Revisions)
            .HasForeignKey(r => r.B2BNegotiationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<MerchantProfile>()
            .WithMany()
            .HasForeignKey(r => r.ProposedByMerchantProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(r => r.Lines).HasField("_lines");
    }
}

public sealed class B2BOfferLineConfiguration : IEntityTypeConfiguration<B2BOfferLine>
{
    public void Configure(EntityTypeBuilder<B2BOfferLine> builder)
    {
        builder.ToTable("B2BOfferLines", table =>
            table.HasCheckConstraint("CK_B2BOfferLines_PositiveQuantity", "[Quantity] > 0"));

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        // One line per variant on a revision.
        builder.HasIndex(l => new { l.B2BOfferRevisionId, l.ListingVariantId }).IsUnique();

        builder.HasOne<B2BOfferRevision>()
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.B2BOfferRevisionId)
            .OnDelete(DeleteBehavior.Cascade);

        // The variant an offer line references must never be hard-deleted out from under the
        // negotiation history.
        builder.HasOne<ListingVariant>()
            .WithMany()
            .HasForeignKey(l => l.ListingVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
