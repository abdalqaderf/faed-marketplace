using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class ListingMediaConfiguration : IEntityTypeConfiguration<ListingMedia>
{
    public void Configure(EntityTypeBuilder<ListingMedia> builder)
    {
        builder.ToTable("ListingMedia");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.StorageObjectKey)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(m => m.OriginalFileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(m => m.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.AltText).HasMaxLength(ListingMedia.MaxAltTextLength);

        builder.Property(m => m.MediaType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Defect photography is queried on its own so disclosure can be surfaced first
        builder.HasIndex(m => new { m.ListingId, m.MediaType, m.SortOrder });

        builder.HasOne(m => m.Listing)
            .WithMany(l => l.Media)
            .HasForeignKey(m => m.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ListingDiscountReasonConfiguration : IEntityTypeConfiguration<ListingDiscountReason>
{
    public void Configure(EntityTypeBuilder<ListingDiscountReason> builder)
    {
        builder.ToTable("ListingDiscountReasons");

        builder.HasKey(x => new { x.ListingId, x.DiscountReasonId });

        builder.HasOne<Listing>()
            .WithMany(l => l.DiscountReasons)
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        // A reason that listings still cite must not be deletable out from under them.
        builder.HasOne(x => x.DiscountReason)
            .WithMany()
            .HasForeignKey(x => x.DiscountReasonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ListingReferencePriceEvidenceConfiguration
    : IEntityTypeConfiguration<ListingReferencePriceEvidence>
{
    public void Configure(EntityTypeBuilder<ListingReferencePriceEvidence> builder)
    {
        builder.ToTable("ListingReferencePriceEvidence");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.EvidenceType)
            .HasConversion<string>()
            .HasMaxLength(48)
            .IsRequired();

        builder.Property(e => e.ReferenceUrl)
            .HasMaxLength(ListingReferencePriceEvidence.MaxReferenceUrlLength);

        builder.Property(e => e.StorageObjectKey).HasMaxLength(400);
        builder.Property(e => e.OriginalFileName).HasMaxLength(260);
        builder.Property(e => e.ContentType).HasMaxLength(100);
        builder.Property(e => e.Note).HasMaxLength(ListingReferencePriceEvidence.MaxNoteLength);

        builder.HasIndex(e => e.ListingId);

        builder.HasOne(e => e.Listing)
            .WithMany(l => l.ReferencePriceEvidence)
            .HasForeignKey(e => e.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
