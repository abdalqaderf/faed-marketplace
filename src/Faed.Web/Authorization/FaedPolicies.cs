namespace Faed.Web.Authorization;

/// <summary>
/// Canonical authorization policy names. No magic strings for policies anywhere else
/// (AGENTS.md §6, docs/08-SECURITY-AND-PRIVACY.md §2).
/// </summary>
public static class FaedPolicies
{
    /// <summary>Requires the <c>Admin</c> role.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Requires an Identity user whose merchant profile is Approved.</summary>
    public const string ApprovedMerchant = "ApprovedMerchant";
}
