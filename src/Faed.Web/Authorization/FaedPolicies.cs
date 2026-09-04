namespace Faed.Web.Authorization;

/// <summary>
/// Canonical authorization policy names. No magic strings for policies anywhere else
/// </summary>
public static class FaedPolicies
{
    /// <summary>Requires the <c>Admin</c> role.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>
    /// Selling authorization: an Identity user whose merchant profile is Approved and who is
    /// <em>not</em> an administrator. An administrator account can never hold a selling
    /// merchant identity — moderation stays independent of the merchants it moderates.
    /// The service layer repeats this check.
    /// </summary>
    public const string ApprovedMerchant = "ApprovedMerchant";

    /// <summary>
    /// Requires an approved merchant who is not an administrator. Administrators may monitor
    /// B2B activity, but cannot participate in negotiations.
    /// </summary>
    public const string CanNegotiateB2B = "CanNegotiateB2B";

    /// <summary>
    /// Requires a Buyer or Merchant role and excludes administrators. Merchant remains an
    /// additive role, so an approved merchant can still act as a consumer
    /// </summary>
    public const string CanPlaceB2COrder = "CanPlaceB2COrder";
}
