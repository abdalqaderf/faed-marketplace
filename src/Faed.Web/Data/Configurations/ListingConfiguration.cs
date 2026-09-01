using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("Listings");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        // Computed helpers on the aggregate, not persisted state.
        builder.Ignore(l => l.IsPubliclyVisible);
        builder.Ignore(l => l.AvailableUnits);
        builder.Ignore(l => l.AcceptsMaterialEdit);
        builder.Ignore(l => l.IsArchived);
        builder.Ignore(l => l.PendingModeration);
        builder.Ignore(l => l.LatestModeration);

        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(Listing.MaxTitleLength);

        builder.Property(l => l.Slug)
            .IsRequired()
            .HasMaxLength(Listing.MaxSlugLength);

        builder.Property(l => l.Description)
            .IsRequired()
            .HasMaxLength(Listing.MaxDescriptionLength);

        builder.Property(l => l.ReturnPolicyText).HasMaxLength(Listing.MaxPolicyTextLength);
        builder.Property(l => l.WarrantyText).HasMaxLength(Listing.MaxPolicyTextLength);
        builder.Property(l => l.IncludedItemsText).HasMaxLength(Listing.MaxPolicyTextLength);
        builder.Property(l => l.MissingItemsText).HasMaxLength(Listing.MaxPolicyTextLength);

        // JOD is stored with three decimal places everywhere (AGENTS.md §6).
        builder.Property(l => l.ReferencePrice).HasColumnType("decimal(18,3)");
        builder.Property(l => l.RetailPrice).HasColumnType("decimal(18,3)");
        builder.Property(l => l.WholesaleIndicativeUnitPrice).HasColumnType("decimal(18,3)");

        // Persist the workflow enum as text so moderation queues and ad-hoc DB reads stay
        // legible (docs/19-CODING-CONVENTIONS.md "Enums vs tables").
        builder.Property(l => l.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Guards a merchant edit racing an admin moderation decision.
        builder.Property(l => l.RowVersion).IsRowVersion();

        builder.HasIndex(l => l.Slug).IsUnique();
        builder.HasIndex(l => new { l.Status, l.CategoryId, l.PublishedAtUtc });
        builder.HasIndex(l => new { l.MerchantProfileId, l.Status });

        // Catalog and merchant references are all restricted: a populated category, grade or
        // merchant must never take listings with it (docs/04-DOMAIN-MODEL.md §12).
        builder.HasOne<MerchantProfile>()
            .WithMany()
            .HasForeignKey(l => l.MerchantProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(l => l.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ConditionGrade>()
            .WithMany()
            .HasForeignKey(l => l.ConditionGradeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Brand>()
            .WithMany()
            .HasForeignKey(l => l.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        // The aggregate owns these collections through backing fields; each child is
        // configured from its own dependent side.
        builder.Navigation(l => l.Options).HasField("_options");
        builder.Navigation(l => l.Variants).HasField("_variants");
        builder.Navigation(l => l.Media).HasField("_media");
        builder.Navigation(l => l.DiscountReasons).HasField("_discountReasons");
        builder.Navigation(l => l.ReferencePriceEvidence).HasField("_referencePriceEvidence");
        builder.Navigation(l => l.Moderations).HasField("_moderations");
    }
}
