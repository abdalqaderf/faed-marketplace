using Microsoft.AspNetCore.Identity;

namespace Faed.Web.Models.Identity;

/// <summary>
/// Application user for ASP.NET Core Identity.
/// Only fields genuinely required by the foundation are added now.
/// MerchantProfile is a later domain concern.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;
}
