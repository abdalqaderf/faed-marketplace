using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class MerchantLocationConfiguration : IEntityTypeConfiguration<MerchantLocation>
{
    public void Configure(EntityTypeBuilder<MerchantLocation> builder)
    {
        builder.ToTable("MerchantLocations");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Name).IsRequired().HasMaxLength(MerchantLocation.MaxNameLength);
        builder.Property(l => l.AddressLine).IsRequired().HasMaxLength(MerchantLocation.MaxAddressLineLength);
        builder.Property(l => l.Area).IsRequired().HasMaxLength(MerchantLocation.MaxAreaLength);
        builder.Property(l => l.City).IsRequired().HasMaxLength(MerchantLocation.MaxCityLength);
        builder.Property(l => l.PickupInstructions).HasMaxLength(MerchantLocation.MaxInstructionsLength);
        builder.Property(l => l.PickupHoursText).HasMaxLength(MerchantLocation.MaxHoursLength);

        builder.HasIndex(l => new { l.MerchantProfileId, l.IsActive });

        builder.HasOne<MerchantProfile>()
            .WithMany()
            .HasForeignKey(l => l.MerchantProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
