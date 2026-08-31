using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class DiscountReasonConfiguration : IEntityTypeConfiguration<DiscountReason>
{
    public void Configure(EntityTypeBuilder<DiscountReason> builder)
    {
        builder.ToTable("DiscountReasons");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Code)
            .IsRequired()
            .HasMaxLength(DiscountReason.MaxCodeLength);

        builder.HasIndex(r => r.Code).IsUnique();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(DiscountReason.MaxNameLength);

        builder.Property(r => r.Description)
            .HasMaxLength(DiscountReason.MaxDescriptionLength);

        // No relationship to ConditionGrade: physical state and discount reason are
        // independent concepts (docs/adr/0003-CONDITION-VS-DISCOUNT-REASON.md).
    }
}
