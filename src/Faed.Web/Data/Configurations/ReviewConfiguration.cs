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
        {
            table.HasCheckConstraint(
                "CK_Reviews_RatingRange",
                $"[Rating] >= {Review.MinRating} AND [Rating] <= {Review.MaxRating}");
            table.HasCheckConstraint(
                "CK_Reviews_ExactlyOneTransaction",
                "(CASE WHEN [OrderId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [B2BDealId] IS NULL THEN 0 ELSE 1 END) = 1");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Ignore(r => r.TransactionType);

        builder.Property(r => r.ReviewerUserId).IsRequired().HasMaxLength(450);
        builder.Property(r => r.Comment).HasMaxLength(Review.MaxCommentLength);

        // "One allowed review per reviewer/transaction". Each
        // transaction has exactly one eligible reviewer, so uniqueness on the transaction FK
        // is the database backstop for the duplicate-review rule.
        // Filtered so the many NULLs on the other FK do not
        // collide.
        builder.HasIndex(r => r.OrderId)
            .IsUnique()
            .HasDatabaseName("IX_Reviews_OrderId_Unique")
            .HasFilter("[OrderId] IS NOT NULL");

        builder.HasIndex(r => r.B2BDealId)
            .IsUnique()
            .HasDatabaseName("IX_Reviews_B2BDealId_Unique")
            .HasFilter("[B2BDealId] IS NOT NULL");

        builder.HasIndex(r => new { r.ReviewedMerchantProfileId, r.CreatedAtUtc });

        // Reviews are transactional history: never cascade-deleted with the merchant, the
        // transaction, or the reviewer.
        builder.HasOne<MerchantProfile>()
            .WithMany()
            .HasForeignKey(r => r.ReviewedMerchantProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<B2BDeal>()
            .WithMany()
            .HasForeignKey(r => r.B2BDealId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
