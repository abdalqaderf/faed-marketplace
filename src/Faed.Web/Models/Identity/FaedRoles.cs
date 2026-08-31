namespace Faed.Web.Models.Identity;

/// <summary>
/// Canonical ASP.NET Core Identity role names for Faed.
/// Roles are an Identity concern; merchant verification is a separate domain state
/// (see docs/08-SECURITY-AND-PRIVACY.md and docs/04-DOMAIN-MODEL.md).
/// No magic strings for roles anywhere else in the codebase (AGENTS.md §6).
/// </summary>
public static class FaedRoles
{
    public const string Buyer = "Buyer";
    public const string Merchant = "Merchant";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = new[] { Buyer, Merchant, Admin };
}
