using Microsoft.AspNetCore.Identity;

namespace Faed.Web.Models.Identity;

/// <summary>
/// Application user for ASP.NET Core Identity.
/// Normal registrations capture the user's basic personal profile.
/// Merchant business information remains a separate verified domain concern.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public const int MaxNameLength = 100;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public string FullName =>
        $"{FirstName} {LastName}".Trim();
}
