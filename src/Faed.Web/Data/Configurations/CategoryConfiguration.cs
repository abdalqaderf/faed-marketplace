using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Ignore(c => c.IsRoot);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(Category.MaxNameLength);

        builder.Property(c => c.Slug)
            .IsRequired()
            .HasMaxLength(Category.MaxSlugLength);

        // Slugs are globally unique public identifiers (docs/04-DOMAIN-MODEL.md §11).
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasIndex(c => new { c.ParentCategoryId, c.SortOrder });

        // Self-referencing hierarchy. Delete is restricted so a populated branch is never
        // silently removed (docs/04-DOMAIN-MODEL.md §12).
        builder.HasMany(c => c.Children)
            .WithOne(c => c.Parent)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
