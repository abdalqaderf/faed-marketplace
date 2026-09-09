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
    /// Merchant-workspace authorization for building, not yet publishing: an Identity user
    /// with a merchant profile that has not been suspended, excluding administrators.
    /// Verification is submitted and approved <em>before</em> a plan is chosen, but drafts are
    /// creatable from registration (<c>BUSINESS-MODEL.md</c> §7.4) — this policy is what lets
    /// an unapproved merchant reach the listing workspace to build them. The publish gate
    /// (verified, subscribed, under quota) is a separate, service-level check with its own
    /// three distinct messages, not folded into this policy.
    /// </summary>
    public const string RegisteredMerchant = "RegisteredMerchant";

    /// <summary>
    /// Requires a Buyer or Merchant role and excludes administrators. Merchant remains an
    /// additive role, so an approved merchant can still act as a consumer
    /// </summary>
    public const string CanPlaceB2COrder = "CanPlaceB2COrder";
}
