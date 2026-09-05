using Faed.Web.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FirstName)
            .HasMaxLength(ApplicationUser.MaxNameLength)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasMaxLength(ApplicationUser.MaxNameLength)
            .IsRequired();

        builder.Property(u => u.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);
    }
}
