using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(Brand.MaxNameLength);

        builder.Property(b => b.Slug)
            .IsRequired()
            .HasMaxLength(Brand.MaxSlugLength);

        // Slugs are globally unique public identifiers (docs/04-DOMAIN-MODEL.md §11).
        builder.HasIndex(b => b.Slug).IsUnique();
    }
}
