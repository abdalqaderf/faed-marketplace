using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class ConditionGradeConfiguration : IEntityTypeConfiguration<ConditionGrade>
{
    public void Configure(EntityTypeBuilder<ConditionGrade> builder)
    {
        builder.ToTable("ConditionGrades");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.Code)
            .IsRequired()
            .HasMaxLength(ConditionGrade.MaxCodeLength);

        builder.HasIndex(g => g.Code).IsUnique();

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(ConditionGrade.MaxNameLength);

        builder.Property(g => g.Description)
            .IsRequired()
            .HasMaxLength(ConditionGrade.MaxDescriptionLength);
    }
}
