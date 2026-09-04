using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class MerchantDeliveryZoneConfiguration : IEntityTypeConfiguration<MerchantDeliveryZone>
{
    public void Configure(EntityTypeBuilder<MerchantDeliveryZone> builder)
    {
        builder.ToTable("MerchantDeliveryZones", table =>
            table.HasCheckConstraint(
                "CK_MerchantDeliveryZones_NonNegativeMoney",
                "[DeliveryFee] >= 0 AND ([MinimumOrderValue] IS NULL OR [MinimumOrderValue] >= 0)"));

        builder.HasKey(z => z.Id);
        builder.Property(z => z.Id).ValueGeneratedNever();

        builder.Property(z => z.Name).IsRequired().HasMaxLength(MerchantDeliveryZone.MaxNameLength);
        builder.Property(z => z.EstimatedDeliveryText).HasMaxLength(MerchantDeliveryZone.MaxEstimateLength);

        // JOD is stored with three decimal places everywhere.
        builder.Property(z => z.DeliveryFee).HasColumnType("decimal(18,3)");
        builder.Property(z => z.MinimumOrderValue).HasColumnType("decimal(18,3)");

        builder.HasIndex(z => new { z.MerchantProfileId, z.IsActive });

        builder.HasOne<MerchantProfile>()
            .WithMany()
            .HasForeignKey(z => z.MerchantProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
