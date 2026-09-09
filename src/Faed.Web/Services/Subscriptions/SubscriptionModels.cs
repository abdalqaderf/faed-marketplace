using Faed.Web.Models.Enums;

namespace Faed.Web.Services.Subscriptions;

/// <summary>One plan a merchant can choose, as shown on the plan chooser.</summary>
public sealed record SubscriptionPlanChoice(
    Guid Id, string Code, string Name, decimal MonthlyPriceJod, int ActiveListingQuota, bool HasFeaturedPlacement);

/// <summary>The merchant's current subscription, with live usage against its quota.</summary>
public sealed record MerchantSubscriptionView(
    Guid Id,
    Guid SubscriptionPlanId,
    string PlanCode,
    string PlanName,
    decimal MonthlyPriceJod,
    int ActiveListingQuota,
    SubscriptionStatus Status,
    DateTime? StartsAtUtc,
    DateTime? ExpiresAtUtc,
    string? PaymentReference,
    int LiveListingCount);

/// <summary>Everything the merchant subscription page needs in one read.</summary>
public sealed record MerchantSubscriptionPageView(
    MerchantSubscriptionView? Subscription,
    IReadOnlyList<SubscriptionPlanChoice> AvailablePlans,
    bool IsVerified);

/// <summary>Outcome of a merchant choosing or changing their plan.</summary>
public sealed record PlanChangeOutcome(int ListingsPaused);

/// <summary>The admin's single-merchant subscription detail: state plus live quota usage.</summary>
public sealed record AdminSubscriptionDetail(
    Guid SubscriptionId,
    Guid MerchantProfileId,
    string BusinessName,
    string PlanName,
    decimal MonthlyPriceJod,
    int ActiveListingQuota,
    SubscriptionStatus Status,
    DateTime? StartsAtUtc,
    DateTime? ExpiresAtUtc,
    string? PaymentReference,
    string? ActivatedByAdminId,
    int LiveListingCount);

/// <summary>One row of the admin subscriptions queue.</summary>
public sealed record SubscriptionQueueItem(
    Guid SubscriptionId,
    Guid MerchantProfileId,
    string BusinessName,
    string PlanName,
    SubscriptionStatus Status,
    DateTime? StartsAtUtc,
    DateTime? ExpiresAtUtc,
    string? PaymentReference);

/// <summary>Which merchant subscriptions the admin queue should return.</summary>
public enum SubscriptionQueueFilter
{
    All = 0,
    Active = 1,
    PendingActivation = 2,
    Expired = 3,
    Cancelled = 4,
}

/// <summary>Which of the publish gate's three independent conditions blocked the merchant.</summary>
public enum PublishGateFailure
{
    NotVerified,
    NotSubscribed,
    QuotaReached,
}

/// <summary>
/// The outcome of the publish gate (<c>CLAUDE.md</c> invariant 7): approved verification,
/// an active subscription, and headroom under the plan's quota, checked and reported
/// independently so the merchant is told exactly which one is missing.
/// </summary>
public sealed record PublishGateResult(bool CanPublish, PublishGateFailure? Failure, string? Message)
{
    public static PublishGateResult Ok() => new(true, null, null);

    public static PublishGateResult NotVerified(string message) =>
        new(false, PublishGateFailure.NotVerified, message);

    public static PublishGateResult NotSubscribed(string message) =>
        new(false, PublishGateFailure.NotSubscribed, message);

    public static PublishGateResult QuotaReached(string message) =>
        new(false, PublishGateFailure.QuotaReached, message);
}
