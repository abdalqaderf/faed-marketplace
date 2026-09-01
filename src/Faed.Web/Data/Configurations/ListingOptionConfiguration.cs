using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class ListingOptionConfiguration : IEntityTypeConfiguration<ListingOption>
{
    public void Configure(EntityTypeBuilder<ListingOption> builder)
    {
        builder.ToTable("ListingOptions");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(ListingOption.MaxNameLength);

        // One "Size" per listing, not two.
        builder.HasIndex(o => new { o.ListingId, o.Name }).IsUnique();

        // Options only describe how a listing varies; they carry no history worth keeping
        // once the listing itself is deleted.
        builder.HasOne<Listing>()
            .WithMany(l => l.Options)
            .HasForeignKey(o => o.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Values).HasField("_values");
    }
}

public sealed class ListingOptionValueConfiguration : IEntityTypeConfiguration<ListingOptionValue>
{
    public void Configure(EntityTypeBuilder<ListingOptionValue> builder)
    {
        builder.ToTable("ListingOptionValues");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.Value)
            .IsRequired()
            .HasMaxLength(ListingOptionValue.MaxValueLength);

        builder.HasIndex(v => new { v.ListingOptionId, v.Value }).IsUnique();

        builder.HasOne(v => v.Option)
            .WithMany(o => o.Values)
            .HasForeignKey(v => v.ListingOptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
