using Faed.Web.Services.Common;

namespace Faed.Web.Services.Subscriptions;

/// <summary>
/// Merchant subscription use cases: choosing a plan, admin collection (activate, extend,
/// cancel), and the publish gate every listing-publishing action must pass
/// (<c>CLAUDE.md</c> invariant 7 — verified <em>and</em> subscribed <em>and</em> under quota,
/// checked and reported independently).
/// </summary>
public interface ISubscriptionService
{
    Task<IReadOnlyList<SubscriptionPlanChoice>> GetAvailablePlansAsync(CancellationToken cancellationToken = default);

    Task<MerchantSubscriptionPageView?> GetMySubscriptionAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The merchant picks or changes their plan. Only available to an approved merchant
    /// (<c>BUSINESS-MODEL.md</c> §7: verify first, then choose a plan). Downgrading an
    /// already-active subscription auto-pauses the listings beyond the new quota, oldest
    /// published first, and reports how many so the caller can tell the merchant.
    /// </summary>
    Task<Result<PlanChangeOutcome>> ChoosePlanAsync(
        string userId, Guid subscriptionPlanId, CancellationToken cancellationToken = default);

    // --- Admin collection ---

    Task<PagedResult<SubscriptionQueueItem>> GetQueueAsync(
        SubscriptionQueueFilter filter, int page = 1, CancellationToken cancellationToken = default);

    Task<AdminSubscriptionDetail?> GetForAdminAsync(Guid merchantProfileId, CancellationToken cancellationToken = default);

    Task<Result> ActivateAsync(
        string adminUserId, Guid merchantProfileId, string paymentReference, CancellationToken cancellationToken = default);

    Task<Result> ExtendAsync(
        string adminUserId, Guid merchantProfileId, string paymentReference, CancellationToken cancellationToken = default);

    /// <summary>Ends the subscription early and hides the merchant's live listings, like expiry.</summary>
    Task<Result> CancelAsync(
        string adminUserId, Guid merchantProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The publish gate. Used by listing submission/restoration and re-checked at admin
    /// approval, so the three conditions are never collapsed into one flag.
    /// </summary>
    Task<PublishGateResult> CheckPublishGateAsync(Guid merchantProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves every <c>Active</c> subscription past its <c>ExpiresAtUtc</c> to
    /// <see cref="Faed.Web.Models.Enums.SubscriptionStatus.Expired"/> and hides that merchant's
    /// live listings — hidden, never archived (<c>CORE.md</c> §3.5). Driven by
    /// <see cref="SubscriptionExpiryService"/>. Returns how many subscriptions expired.
    /// </summary>
    Task<int> ExpireDueSubscriptionsAsync(CancellationToken cancellationToken = default);
}
