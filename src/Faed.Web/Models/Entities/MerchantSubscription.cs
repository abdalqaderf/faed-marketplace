using Faed.Web.Models;
using Faed.Web.Models.Enums;

namespace Faed.Web.Models.Entities;

/// <summary>
/// One merchant's subscription state, 1:1 with <see cref="MerchantProfile"/>. Collection is
/// manual (<c>BUSINESS-MODEL.md</c> §6.4): an admin records a payment reference and activates
/// or extends the period. There is no payment gateway and no balance — this row only tracks
/// the period an admin has vouched for.
/// <see cref="RowVersion"/> guards two admins recording competing decisions on the same
/// subscription.
/// </summary>
public class MerchantSubscription
{
    private MerchantSubscription()
    {
    }

    public MerchantSubscription(Guid merchantProfileId, Guid subscriptionPlanId, DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        MerchantProfileId = merchantProfileId;
        SubscriptionPlanId = subscriptionPlanId;
        Status = SubscriptionStatus.PendingActivation;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public Guid MerchantProfileId { get; private set; }

    public Guid SubscriptionPlanId { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public DateTime? StartsAtUtc { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public string? PaymentReference { get; private set; }

    public string? ActivatedByAdminId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Optimistic concurrency token guarding two competing admin decisions.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>The one condition of the publish gate this entity is responsible for.</summary>
    public bool CanPublish => Status == SubscriptionStatus.Active;

    /// <summary>
    /// The merchant picks or changes their plan. A merchant whose period lapsed
    /// (<see cref="SubscriptionStatus.Expired"/> or <see cref="SubscriptionStatus.Cancelled"/>)
    /// starts a fresh, unpaid period on the new plan; an <see cref="SubscriptionStatus.Active"/>
    /// merchant simply swaps plans mid-period — the new quota applies immediately, which is
    /// what makes a downgrade take effect without waiting for renewal.
    /// </summary>
    public void ChoosePlan(Guid subscriptionPlanId, DateTime nowUtc)
    {
        if (Status is SubscriptionStatus.Expired or SubscriptionStatus.Cancelled)
        {
            Status = SubscriptionStatus.PendingActivation;
            StartsAtUtc = null;
            ExpiresAtUtc = null;
            PaymentReference = null;
            ActivatedByAdminId = null;
        }

        SubscriptionPlanId = subscriptionPlanId;
        Touch(nowUtc);
    }

    /// <summary>
    /// An admin records a payment and starts (or restarts, after expiry or cancellation) a
    /// one-month period from now. Monthly only — no other period is ever offered
    /// (<c>BUSINESS-MODEL.md</c> §6.2).
    /// </summary>
    public void Activate(string adminUserId, string paymentReference, DateTime nowUtc)
    {
        if (Status == SubscriptionStatus.Active)
        {
            throw new DomainException("This subscription is already active. Extend it instead of activating it.");
        }

        Status = SubscriptionStatus.Active;
        StartsAtUtc = nowUtc;
        ExpiresAtUtc = nowUtc.AddMonths(1);
        PaymentReference = RequireReference(paymentReference);
        ActivatedByAdminId = RequireAdminId(adminUserId);
        Touch(nowUtc);
    }

    /// <summary>
    /// An admin records a renewal payment for an already-active subscription, adding one month
    /// to whichever is later: the current expiry (renewing early keeps the remaining days) or
    /// now (renewing late does not back-date extra time).
    /// </summary>
    public void Extend(string adminUserId, string paymentReference, DateTime nowUtc)
    {
        if (Status != SubscriptionStatus.Active)
        {
            throw new DomainException($"A subscription in status {Status} cannot be extended. Activate it instead.");
        }

        var baseline = ExpiresAtUtc is { } expires && expires > nowUtc ? expires : nowUtc;
        ExpiresAtUtc = baseline.AddMonths(1);
        PaymentReference = RequireReference(paymentReference);
        ActivatedByAdminId = RequireAdminId(adminUserId);
        Touch(nowUtc);
    }

    /// <summary>An admin ends the subscription early. Has the same publishing effect as expiry.</summary>
    public void Cancel(string adminUserId, DateTime nowUtc)
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            throw new DomainException($"A subscription in status {Status} is already inactive.");
        }

        _ = RequireAdminId(adminUserId);
        Status = SubscriptionStatus.Cancelled;
        Touch(nowUtc);
    }

    /// <summary>The background expiry sweep moves a period that reached its end date to Expired.</summary>
    public void Expire(DateTime nowUtc)
    {
        if (Status != SubscriptionStatus.Active)
        {
            throw new DomainException($"A subscription in status {Status} cannot expire.");
        }

        Status = SubscriptionStatus.Expired;
        Touch(nowUtc);
    }

    private static string RequireReference(string paymentReference)
    {
        if (string.IsNullOrWhiteSpace(paymentReference))
        {
            throw new DomainException("A payment reference is required to record this payment.");
        }

        var trimmed = paymentReference.Trim();
        return trimmed.Length > 200
            ? throw new DomainException("The payment reference must be 200 characters or fewer.")
            : trimmed;
    }

    private static string RequireAdminId(string adminUserId) =>
        string.IsNullOrWhiteSpace(adminUserId)
            ? throw new DomainException("An admin user id is required to record a subscription decision.")
            : adminUserId;

    private void Touch(DateTime nowUtc) => UpdatedAtUtc = nowUtc;
}
