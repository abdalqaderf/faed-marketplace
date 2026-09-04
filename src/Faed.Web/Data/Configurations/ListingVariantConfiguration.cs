using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class ListingVariantConfiguration : IEntityTypeConfiguration<ListingVariant>
{
    public void Configure(EntityTypeBuilder<ListingVariant> builder)
    {
        builder.ToTable("ListingVariants", table =>
        {
            // Quantities are guarded at the strongest available layer as well as in the
            // aggregate, so no code path — present or future — can persist negative stock
            table.HasCheckConstraint("CK_ListingVariants_Quantities_NonNegative",
                "[InitialQuantity] >= 0 AND [AvailableQuantity] >= 0 " +
                "AND [ReservedQuantity] >= 0 AND [SoldQuantity] >= 0");
        });

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Ignore(v => v.IsSellable);

        builder.Property(v => v.Sku)
            .IsRequired()
            .HasMaxLength(ListingVariant.MaxSkuLength);

        builder.Property(v => v.OptionCombinationKey)
            .IsRequired()
            .HasMaxLength(ListingVariant.MaxOptionCombinationKeyLength);

        // Optimistic concurrency is present from the first variant migration: two requests
        // for the last unit must not both succeed.
        builder.Property(v => v.RowVersion).IsRowVersion();

        builder.HasIndex(v => new { v.ListingId, v.Sku }).IsUnique();

        // "One Listing cannot have duplicate option-value combinations"
        // — enforced by the database, not only by the aggregate.
        builder.HasIndex(v => new { v.ListingId, v.OptionCombinationKey })
            .IsUnique()
            .HasDatabaseName("IX_ListingVariants_ListingId_OptionCombinationKey");

        builder.HasIndex(v => new { v.ListingId, v.IsActive });

        builder.HasOne<Listing>()
            .WithMany(l => l.Variants)
            .HasForeignKey(v => v.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(v => v.OptionValues).HasField("_optionValues");
    }
}

public sealed class ListingVariantOptionValueConfiguration : IEntityTypeConfiguration<ListingVariantOptionValue>
{
    public void Configure(EntityTypeBuilder<ListingVariantOptionValue> builder)
    {
        builder.ToTable("ListingVariantOptionValues");

        builder.HasKey(x => new { x.ListingVariantId, x.ListingOptionValueId });

        builder.HasOne<ListingVariant>()
            .WithMany(v => v.OptionValues)
            .HasForeignKey(x => x.ListingVariantId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction rather than Cascade: SQL Server rejects the second cascade path that would
        // otherwise reach this table (Listing → Option → OptionValue as well as
        // Listing → Variant). The aggregate already refuses to remove an option value that a
        // variant still uses, so no orphan can be created.
        builder.HasOne(x => x.OptionValue)
            .WithMany()
            .HasForeignKey(x => x.ListingOptionValueId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
