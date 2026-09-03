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

    /// <summary>
    /// Requires an approved merchant who is not an administrator. Administrators may monitor
    /// B2B activity in a later task, but cannot participate in negotiations.
    /// </summary>
    public const string CanNegotiateB2B = "CanNegotiateB2B";

    /// <summary>
    /// Requires an authenticated user who is not an administrator. Individual buyers (and
    /// merchants acting as consumers) may place B2C orders; administrators may not
    /// (docs/16-PERMISSIONS-MATRIX.md "Create B2C order — Admin ❌").
    /// </summary>
    public const string CanPlaceB2COrder = "CanPlaceB2COrder";
}
