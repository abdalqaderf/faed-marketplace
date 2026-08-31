using Faed.Web.Models.Entities;
using Faed.Web.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class MerchantProfileConfiguration : IEntityTypeConfiguration<MerchantProfile>
{
    public void Configure(EntityTypeBuilder<MerchantProfile> builder)
    {
        builder.ToTable("MerchantProfiles");

        builder.HasKey(p => p.Id);

        // Identity is assigned by the domain constructor (Guid v7), never by the store.
        builder.Property(p => p.Id).ValueGeneratedNever();

        // Computed helpers on the aggregate, not persisted state.
        builder.Ignore(p => p.ActiveDocuments);
        builder.Ignore(p => p.CanSell);
        builder.Ignore(p => p.IsEditable);

        builder.Property(p => p.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(p => p.BusinessName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.PublicSlug)
            .IsRequired()
            .HasMaxLength(220);

        builder.Property(p => p.ContactEmail).HasMaxLength(256);
        builder.Property(p => p.ContactPhone).HasMaxLength(32);
        builder.Property(p => p.RejectionReason).HasMaxLength(MerchantProfile.MaxDecisionReasonLength);
        builder.Property(p => p.ReviewedByAdminId).HasMaxLength(450);

        // Optimistic concurrency for competing admin verification decisions.
        builder.Property(p => p.RowVersion).IsRowVersion();

        // Persist the workflow enum as text so the audit trail and ad-hoc DB reads stay legible
        // (docs/19-CODING-CONVENTIONS.md "Enums vs tables").
        builder.Property(p => p.VerificationStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(p => p.UserId).IsUnique();
        builder.HasIndex(p => p.PublicSlug).IsUnique();
        builder.HasIndex(p => p.VerificationStatus);

        // 1:1 with the Identity user. No navigation on the domain side; delete is restricted
        // so business history is never cascade-removed with an account
        // (docs/04-DOMAIN-MODEL.md §12).
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<MerchantProfile>(p => p.UserId)
            .HasPrincipalKey<ApplicationUser>(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);

        // The Documents collection is read-only over the `_documents` backing field, which
        // EF discovers by convention. The relationship is configured from the dependent
        // side in MerchantVerificationDocumentConfiguration.
    }
}
