namespace Faed.Web.Models.Identity;

/// <summary>
/// Canonical ASP.NET Core Identity role names for Faed.
/// Roles are an Identity concern; merchant verification is a separate domain state.
/// No magic strings for roles anywhere else in the codebase.
/// </summary>
public static class FaedRoles
{
    public const string Buyer = "Buyer";
    public const string Merchant = "Merchant";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = new[] { Buyer, Merchant, Admin };
}
