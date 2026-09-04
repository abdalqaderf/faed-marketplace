using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class AdminActionLogConfiguration : IEntityTypeConfiguration<AdminActionLog>
{
    public void Configure(EntityTypeBuilder<AdminActionLog> builder)
    {
        builder.ToTable("AdminActionLogs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.AdminUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(l => l.ActionType)
            .HasConversion<string>()
            .HasMaxLength(48)
            .IsRequired();

        builder.Property(l => l.TargetType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(l => l.TargetId)
            .IsRequired()
            .HasMaxLength(64);

        // Unbounded: an audit note must record the administrator's full text — e.g. a dispute
        // resolution up to Dispute.MaxResolutionLength (4000) — never a silently truncated
        // copy. The status change and its complete audit row commit in
        // one transaction.
        builder.Property(l => l.Notes);

        builder.HasIndex(l => new { l.TargetType, l.TargetId });
        builder.HasIndex(l => l.CreatedAtUtc);
    }
}
