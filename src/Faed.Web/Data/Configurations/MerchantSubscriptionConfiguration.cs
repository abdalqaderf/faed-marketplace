using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class MerchantSubscriptionConfiguration : IEntityTypeConfiguration<MerchantSubscription>
{
    public void Configure(EntityTypeBuilder<MerchantSubscription> builder)
    {
        builder.ToTable("MerchantSubscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Ignore(s => s.CanPublish);

        builder.Property(s => s.PaymentReference).HasMaxLength(200);
        builder.Property(s => s.ActivatedByAdminId).HasMaxLength(450);

        // Optimistic concurrency for two admins recording competing decisions.
        builder.Property(s => s.RowVersion).IsRowVersion();

        // Persist the workflow enum as text so the admin screen and ad-hoc DB reads stay legible.
        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // One subscription row per merchant — a state machine, not a payment history.
        builder.HasIndex(s => s.MerchantProfileId).IsUnique();
        builder.HasIndex(s => new { s.Status, s.ExpiresAtUtc });

        builder.HasOne<MerchantProfile>()
            .WithOne()
            .HasForeignKey<MerchantSubscription>(s => s.MerchantProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SubscriptionPlan>()
            .WithMany()
            .HasForeignKey(s => s.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
