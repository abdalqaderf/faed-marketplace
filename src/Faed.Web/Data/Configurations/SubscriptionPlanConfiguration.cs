using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlans", table =>
            table.HasCheckConstraint(
                "CK_SubscriptionPlans_Positive",
                "[MonthlyPriceJod] >= 0 AND [ActiveListingQuota] >= 1"));

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(SubscriptionPlan.MaxCodeLength);

        builder.HasIndex(p => p.Code).IsUnique();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(SubscriptionPlan.MaxNameLength);

        // JOD is stored with three decimal places everywhere.
        builder.Property(p => p.MonthlyPriceJod).HasColumnType("decimal(18,3)");
    }
}
