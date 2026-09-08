using Faed.Web.Models.Entities;
using Faed.Web.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews", table =>
            table.HasCheckConstraint(
                "CK_Reviews_RatingRange",
                $"[Rating] >= {Review.MinRating} AND [Rating] <= {Review.MaxRating}"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.ReviewerUserId).IsRequired().HasMaxLength(450);
        builder.Property(r => r.Comment).HasMaxLength(Review.MaxCommentLength);
        builder.Property(r => r.OrderId).IsRequired();

        // "One allowed review per order". Each order has exactly one eligible reviewer, so a
        // unique index on the order FK is the database backstop for the duplicate-review rule.
        builder.HasIndex(r => r.OrderId)
            .IsUnique()
            .HasDatabaseName("IX_Reviews_OrderId_Unique");

        builder.HasIndex(r => new { r.ReviewedMerchantProfileId, r.CreatedAtUtc });

        // Reviews are transactional history: never cascade-deleted with the merchant, the
        // order, or the reviewer.
        builder.HasOne<MerchantProfile>()
            .WithMany()
            .HasForeignKey(r => r.ReviewedMerchantProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
