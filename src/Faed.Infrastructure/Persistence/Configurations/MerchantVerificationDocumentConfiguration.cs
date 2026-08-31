using Faed.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Infrastructure.Persistence.Configurations;

public sealed class MerchantVerificationDocumentConfiguration : IEntityTypeConfiguration<MerchantVerificationDocument>
{
    public void Configure(EntityTypeBuilder<MerchantVerificationDocument> builder)
    {
        builder.ToTable("MerchantVerificationDocuments");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        // Owned by exactly one merchant application; removed with it (docs/04-DOMAIN-MODEL.md §12).
        builder.HasOne<MerchantProfile>()
            .WithMany(p => p.Documents)
            .HasForeignKey(d => d.MerchantProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(d => d.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        // Protected storage key only — never a public URL (docs/08-SECURITY-AND-PRIVACY.md §3).
        builder.Property(d => d.StorageObjectKey)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(d => d.OriginalFileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(d => d.MerchantProfileId);
    }
}
