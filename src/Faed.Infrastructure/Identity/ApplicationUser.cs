using Microsoft.AspNetCore.Identity;

namespace Faed.Infrastructure.Identity;

/// <summary>
/// Application user for ASP.NET Core Identity.
/// Only fields genuinely required by the foundation are added now
/// (TASK-001 / docs/04-DOMAIN-MODEL.md §1). MerchantProfile is a later domain concern.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;
}
