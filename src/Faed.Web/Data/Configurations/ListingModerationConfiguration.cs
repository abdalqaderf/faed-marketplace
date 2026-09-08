using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class ListingModerationConfiguration : IEntityTypeConfiguration<ListingModeration>
{
    public void Configure(EntityTypeBuilder<ListingModeration> builder)
    {
        builder.ToTable("ListingModerations");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Ignore(m => m.IsPending);

        builder.Property(m => m.ReasonForReview)
            .IsRequired()
            .HasMaxLength(ListingModeration.MaxReasonForReviewLength);

        builder.Property(m => m.ReviewNote).HasMaxLength(ListingModeration.MaxReviewNoteLength);
        builder.Property(m => m.ReviewedByAdminId).HasMaxLength(450);

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Backs the admin moderation queue, oldest submission first.
        builder.HasIndex(m => new { m.Status, m.SubmittedAtUtc });
        builder.HasIndex(m => m.ListingId);

        builder.HasOne<Listing>()
            .WithMany(l => l.Moderations)
            .HasForeignKey(m => m.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
