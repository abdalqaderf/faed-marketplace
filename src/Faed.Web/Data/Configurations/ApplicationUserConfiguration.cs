using Faed.Web.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faed.Web.Data.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Stamp the creation time in the database so it is set regardless of which
        // code path creates the account (the generated Identity UI does not set it).
        builder.Property(u => u.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);
    }
}
