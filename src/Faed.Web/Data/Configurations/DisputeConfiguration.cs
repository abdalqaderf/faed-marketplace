using Faed.Web.Models.Entities;
using Faed.Web.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.ToTable("Disputes", table =>
            table.HasCheckConstraint(
                "CK_Disputes_ExactlyOneTransaction",
                "(CASE WHEN [OrderId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [B2BDealId] IS NULL THEN 0 ELSE 1 END) = 1"));

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Ignore(d => d.TransactionType);
        builder.Ignore(d => d.IsTerminal);
        builder.Ignore(d => d.AcceptsEvidence);

        // Persist the workflow enums as text so the dispute queue and ad-hoc DB reads stay
        // legible (docs/19-CODING-CONVENTIONS.md "Enums vs tables").
        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.ReasonCode)
            .HasConversion<string>()
            .HasMaxLength(48)
            .IsRequired();

        builder.Property(d => d.RaisedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(d => d.ResolvedByAdminId).HasMaxLength(450);
        builder.Property(d => d.Description).IsRequired().HasMaxLength(Dispute.MaxDescriptionLength);
        builder.Property(d => d.AdminResolution).HasMaxLength(Dispute.MaxResolutionLength);
        builder.Property(d => d.ActiveTransactionKey).HasMaxLength(Dispute.MaxActiveTransactionKeyLength);

        // At most one active (Open/UnderReview) dispute per transaction, enforced by the
        // database so two concurrent filings cannot both win (docs/03-BUSINESS-RULES.md §14,
        // AGENTS.md §7). The key is null for closed disputes, so the filter keeps those out.
        builder.HasIndex(d => d.ActiveTransactionKey)
            .IsUnique()
            .HasDatabaseName("IX_Disputes_ActiveTransactionKey_Unique")
            .HasFilter("[ActiveTransactionKey] IS NOT NULL");

        // Guards two administrators acting on the same dispute at once (AGENTS.md §7).
        builder.Property(d => d.RowVersion).IsRowVersion();

        // Admin queue and "my disputes" reads.
        builder.HasIndex(d => new { d.Status, d.CreatedAtUtc });
        builder.HasIndex(d => d.RaisedByUserId);
        builder.HasIndex(d => d.OrderId);
        builder.HasIndex(d => d.B2BDealId);

        // Transactional history is preserved, never cascade-deleted with a transaction or the
        // Identity user (docs/04-DOMAIN-MODEL.md §12 "Do not cascade-delete ... Disputes").
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<B2BDeal>()
            .WithMany()
            .HasForeignKey(d => d.B2BDealId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.RaisedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(d => d.Evidence).HasField("_evidence");
    }
}

public sealed class DisputeEvidenceConfiguration : IEntityTypeConfiguration<DisputeEvidence>
{
    public void Configure(EntityTypeBuilder<DisputeEvidence> builder)
    {
        builder.ToTable("DisputeEvidence");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.UploadedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(e => e.StorageObjectKey).IsRequired().HasMaxLength(512);
        builder.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(DisputeEvidence.MaxOriginalFileNameLength);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(DisputeEvidence.MaxContentTypeLength);

        builder.HasIndex(e => e.DisputeId);

        // Evidence rows follow their dispute; the dispute itself is never deleted.
        builder.HasOne<Dispute>()
            .WithMany(d => d.Evidence)
            .HasForeignKey(e => e.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
