using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faed.Web.Services.Subscriptions;

/// <inheritdoc />
public sealed class SubscriptionService(
    IApplicationDbContext db,
    ISubscriptionBilling billing,
    IUserRoleService userRoles,
    IClock clock,
    ILogger<SubscriptionService> logger) : ISubscriptionService
{
    public async Task<IReadOnlyList<SubscriptionPlanChoice>> GetAvailablePlansAsync(
        CancellationToken cancellationToken = default) =>
        await db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => new SubscriptionPlanChoice(
                p.Id, p.Code, p.Name, p.MonthlyPriceJod, p.ActiveListingQuota, p.HasFeaturedPlacement))
            .ToListAsync(cancellationToken);

    public async Task<MerchantSubscriptionPageView?> GetMySubscriptionAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var profile = await db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Id, p.VerificationStatus })
            .SingleOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var plans = await GetAvailablePlansAsync(cancellationToken);

        var subscription = await db.MerchantSubscriptions
            .AsNoTracking()
            .Where(s => s.MerchantProfileId == profile.Id)
            .Select(s => new
            {
                s.Id,
                s.SubscriptionPlanId,
                s.Status,
                s.StartsAtUtc,
                s.ExpiresAtUtc,
                s.PaymentReference,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (subscription is null)
        {
            return new MerchantSubscriptionPageView(
                null, plans, profile.VerificationStatus == MerchantVerificationStatus.Approved);
        }

        var plan = await db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.Id == subscription.SubscriptionPlanId)
            .Select(p => new { p.Code, p.Name, p.MonthlyPriceJod, p.ActiveListingQuota })
            .SingleAsync(cancellationToken);

        var liveCount = await CountLiveListingsAsync(profile.Id, cancellationToken);

        var view = new MerchantSubscriptionView(
            subscription.Id, subscription.SubscriptionPlanId, plan.Code, plan.Name, plan.MonthlyPriceJod,
            plan.ActiveListingQuota, subscription.Status, subscription.StartsAtUtc, subscription.ExpiresAtUtc,
            subscription.PaymentReference, liveCount);

        return new MerchantSubscriptionPageView(
            view, plans, profile.VerificationStatus == MerchantVerificationStatus.Approved);
    }

    public async Task<Result<PlanChangeOutcome>> ChoosePlanAsync(
        string userId, Guid subscriptionPlanId, CancellationToken cancellationToken = default)
    {
        var profile = await db.MerchantProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            return Result<PlanChangeOutcome>.Validation("Complete your merchant application before choosing a plan.");
        }

        // BUSINESS-MODEL.md §7: verify first, then choose a plan. The two conditions are
        // independent, but their order is not.
        if (profile.VerificationStatus != MerchantVerificationStatus.Approved)
        {
            return Result<PlanChangeOutcome>.Forbidden(
                "Your merchant application must be approved before you can choose a plan.");
        }

        var plan = await db.SubscriptionPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == subscriptionPlanId && p.IsActive, cancellationToken);

        if (plan is null)
        {
            return Result<PlanChangeOutcome>.Validation("Choose a valid plan.");
        }

        var subscription = await db.MerchantSubscriptions
            .SingleOrDefaultAsync(s => s.MerchantProfileId == profile.Id, cancellationToken);

        var wasActive = subscription?.Status == SubscriptionStatus.Active;

        if (subscription is null)
        {
            subscription = new MerchantSubscription(profile.Id, plan.Id, clock.UtcNow);
            db.MerchantSubscriptions.Add(subscription);
        }
        else
        {
            try
            {
                subscription.ChoosePlan(plan.Id, clock.UtcNow);
            }
            catch (DomainException ex)
            {
                return Result<PlanChangeOutcome>.Validation(ex.Message);
            }
        }

        // Downgrading (or moving sideways) an already-active subscription applies the new
        // quota immediately, per BUSINESS-MODEL.md §6.6: the most recently published listings
        // up to the new quota stay live, the rest are auto-paused. Nothing is deleted.
        var listingsPaused = wasActive
            ? await PauseListingsBeyondQuotaAsync(profile.Id, plan.ActiveListingQuota, clock.UtcNow, cancellationToken)
            : 0;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PlanChangeOutcome>.Conflict("Your subscription changed elsewhere. Reload and try again.");
        }

        logger.LogInformation(
            "Merchant {MerchantId} chose plan {PlanCode}, pausing {Paused} listing(s) over quota",
            profile.Id, plan.Code, listingsPaused);

        return Result<PlanChangeOutcome>.Success(new PlanChangeOutcome(listingsPaused));
    }

    public Task<PagedResult<SubscriptionQueueItem>> GetQueueAsync(
        SubscriptionQueueFilter filter, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = db.MerchantSubscriptions.AsNoTracking();

        query = filter switch
        {
            SubscriptionQueueFilter.Active => query.Where(s => s.Status == SubscriptionStatus.Active),
            SubscriptionQueueFilter.PendingActivation => query.Where(s => s.Status == SubscriptionStatus.PendingActivation),
            SubscriptionQueueFilter.Expired => query.Where(s => s.Status == SubscriptionStatus.Expired),
            SubscriptionQueueFilter.Cancelled => query.Where(s => s.Status == SubscriptionStatus.Cancelled),
            _ => query,
        };

        return query
            // Awaiting a first payment is the work queue; everything else is reference.
            .OrderBy(s => s.Status == SubscriptionStatus.PendingActivation ? 0 : 1)
            .ThenBy(s => s.ExpiresAtUtc ?? DateTime.MaxValue)
            .Select(s => new SubscriptionQueueItem(
                s.Id,
                s.MerchantProfileId,
                db.MerchantProfiles.Where(p => p.Id == s.MerchantProfileId)
                    .Select(p => p.BusinessName).FirstOrDefault() ?? "Unknown merchant",
                db.SubscriptionPlans.Where(p => p.Id == s.SubscriptionPlanId)
                    .Select(p => p.Name).FirstOrDefault() ?? "Unknown plan",
                s.Status,
                s.StartsAtUtc,
                s.ExpiresAtUtc,
                s.PaymentReference))
            .ToPagedResultAsync(page, Paging.AdminPageSize, cancellationToken);
    }

    public async Task<AdminSubscriptionDetail?> GetForAdminAsync(
        Guid merchantProfileId, CancellationToken cancellationToken = default)
    {
        var profile = await db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.Id == merchantProfileId)
            .Select(p => p.BusinessName)
            .SingleOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var subscription = await db.MerchantSubscriptions
            .AsNoTracking()
            .Where(s => s.MerchantProfileId == merchantProfileId)
            .SingleOrDefaultAsync(cancellationToken);

        if (subscription is null)
        {
            return null;
        }

        var plan = await db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.Id == subscription.SubscriptionPlanId)
            .Select(p => new { p.Name, p.MonthlyPriceJod, p.ActiveListingQuota })
            .SingleAsync(cancellationToken);

        var liveCount = await CountLiveListingsAsync(merchantProfileId, cancellationToken);

        return new AdminSubscriptionDetail(
            subscription.Id, merchantProfileId, profile, plan.Name, plan.MonthlyPriceJod, plan.ActiveListingQuota,
            subscription.Status, subscription.StartsAtUtc, subscription.ExpiresAtUtc, subscription.PaymentReference,
            subscription.ActivatedByAdminId, liveCount);
    }

    public async Task<Result> ActivateAsync(
        string adminUserId, Guid merchantProfileId, string paymentReference, CancellationToken cancellationToken = default)
    {
        if (!await userRoles.IsInRoleAsync(adminUserId, FaedRoles.Admin, cancellationToken))
        {
            return Result.Forbidden();
        }

        var subscription = await db.MerchantSubscriptions
            .SingleOrDefaultAsync(s => s.MerchantProfileId == merchantProfileId, cancellationToken);

        if (subscription is null)
        {
            return Result.NotFound("This merchant has not chosen a plan yet.");
        }

        var plan = await db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.Id == subscription.SubscriptionPlanId)
            .Select(p => new { p.MonthlyPriceJod, p.ActiveListingQuota })
            .SingleAsync(cancellationToken);

        var confirmed = await billing.ConfirmPaymentAsync(merchantProfileId, plan.MonthlyPriceJod, paymentReference, cancellationToken);
        if (confirmed.Failed)
        {
            return Result.From(confirmed);
        }

        try
        {
            subscription.Activate(adminUserId, confirmed.Value, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Conflict(ex.Message);
        }

        // BUSINESS-MODEL.md §6.5: renewal is one click, not one click per listing. Whatever a
        // lapse hid returns automatically, most recently published first, capped at the plan's
        // current quota; anything a downgrade paused on purpose is left for the merchant to
        // restore by hand.
        var restored = await RestoreListingsHiddenByLapseAsync(
            merchantProfileId, plan.ActiveListingQuota, clock.UtcNow, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("This subscription was updated by someone else. Reload and try again.");
        }

        logger.LogInformation(
            "Admin {AdminId} activated the subscription for merchant {MerchantId}, restoring {Restored} lapsed listing(s)",
            adminUserId, merchantProfileId, restored);
        return Result.Success();
    }

    public Task<Result> ExtendAsync(
        string adminUserId, Guid merchantProfileId, string paymentReference, CancellationToken cancellationToken = default) =>
        CollectAsync(
            adminUserId, merchantProfileId, paymentReference,
            (subscription, reference, now) => subscription.Extend(adminUserId, reference, now),
            "extend",
            cancellationToken);

    public async Task<Result> CancelAsync(
        string adminUserId, Guid merchantProfileId, CancellationToken cancellationToken = default)
    {
        if (!await userRoles.IsInRoleAsync(adminUserId, FaedRoles.Admin, cancellationToken))
        {
            return Result.Forbidden();
        }

        var subscription = await db.MerchantSubscriptions
            .SingleOrDefaultAsync(s => s.MerchantProfileId == merchantProfileId, cancellationToken);

        if (subscription is null)
        {
            return Result.NotFound("This merchant has not chosen a plan yet.");
        }

        try
        {
            subscription.Cancel(adminUserId, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Conflict(ex.Message);
        }

        // Cancellation has the same publishing effect as expiry: hidden, never deleted.
        await HideLiveListingsForLapseAsync(merchantProfileId, clock.UtcNow, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("This subscription was updated by someone else. Reload and try again.");
        }

        logger.LogInformation("Admin {AdminId} cancelled the subscription for merchant {MerchantId}", adminUserId, merchantProfileId);
        return Result.Success();
    }

    public async Task<PublishGateResult> CheckPublishGateAsync(
        Guid merchantProfileId, CancellationToken cancellationToken = default)
    {
        var profile = await db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.Id == merchantProfileId)
            .Select(p => new { p.VerificationStatus })
            .SingleOrDefaultAsync(cancellationToken);

        if (profile is null || profile.VerificationStatus != MerchantVerificationStatus.Approved)
        {
            return PublishGateResult.NotVerified(
                "Your merchant account must be approved before you can publish listings.");
        }

        var subscription = await db.MerchantSubscriptions
            .AsNoTracking()
            .Where(s => s.MerchantProfileId == merchantProfileId)
            .Select(s => new { s.Status, s.SubscriptionPlanId })
            .SingleOrDefaultAsync(cancellationToken);

        if (subscription is null || subscription.Status != SubscriptionStatus.Active)
        {
            return PublishGateResult.NotSubscribed(
                "You need an active subscription to publish. Choose a plan on your Subscription page.");
        }

        var quota = await db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.Id == subscription.SubscriptionPlanId)
            .Select(p => p.ActiveListingQuota)
            .SingleAsync(cancellationToken);

        var liveCount = await CountLiveListingsAsync(merchantProfileId, cancellationToken);

        return liveCount >= quota
            ? PublishGateResult.QuotaReached($"You've reached your plan limit ({quota} listings). Pause one or upgrade to publish.")
            : PublishGateResult.Ok();
    }

    public async Task<int> ExpireDueSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var dueIds = await db.MerchantSubscriptions
            .AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.Active && s.ExpiresAtUtc < clock.UtcNow)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var id in dueIds)
        {
            DetachTrackedGraph();

            var subscription = await db.MerchantSubscriptions
                .SingleOrDefaultAsync(s => s.Id == id && s.Status == SubscriptionStatus.Active, cancellationToken);

            // Renewed or cancelled by an admin since the id list was taken.
            if (subscription is null || subscription.ExpiresAtUtc is not { } expiresAt || expiresAt >= clock.UtcNow)
            {
                continue;
            }

            try
            {
                subscription.Expire(clock.UtcNow);
                await HideLiveListingsForLapseAsync(subscription.MerchantProfileId, clock.UtcNow, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                expired++;
                logger.LogInformation("Expired subscription {SubscriptionId} for merchant {MerchantId}", id, subscription.MerchantProfileId);
            }
            catch (DbUpdateConcurrencyException)
            {
                // An admin extended it in the same moment; nothing to do.
                logger.LogInformation("Expiry for subscription {SubscriptionId} was superseded", id);
            }
        }

        return expired;
    }

    // ---- Internals ------------------------------------------------------------------

    private async Task<Result> CollectAsync(
        string adminUserId,
        Guid merchantProfileId,
        string paymentReference,
        Action<MerchantSubscription, string, DateTime> apply,
        string action,
        CancellationToken cancellationToken)
    {
        if (!await userRoles.IsInRoleAsync(adminUserId, FaedRoles.Admin, cancellationToken))
        {
            return Result.Forbidden();
        }

        var subscription = await db.MerchantSubscriptions
            .SingleOrDefaultAsync(s => s.MerchantProfileId == merchantProfileId, cancellationToken);

        if (subscription is null)
        {
            return Result.NotFound("This merchant has not chosen a plan yet.");
        }

        var plan = await db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.Id == subscription.SubscriptionPlanId)
            .Select(p => p.MonthlyPriceJod)
            .SingleAsync(cancellationToken);

        var confirmed = await billing.ConfirmPaymentAsync(merchantProfileId, plan, paymentReference, cancellationToken);
        if (confirmed.Failed)
        {
            return Result.From(confirmed);
        }

        try
        {
            apply(subscription, confirmed.Value, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Conflict(ex.Message);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("This subscription was updated by someone else. Reload and try again.");
        }

        logger.LogInformation(
            "Admin {AdminId} performed {Action} on the subscription for merchant {MerchantId}",
            adminUserId, action, merchantProfileId);
        return Result.Success();
    }

    private Task<int> CountLiveListingsAsync(Guid merchantProfileId, CancellationToken cancellationToken) =>
        db.Listings
            .AsNoTracking()
            .CountAsync(
                l => l.MerchantProfileId == merchantProfileId
                    && (l.Status == ListingStatus.Live || l.Status == ListingStatus.SoldOut),
                cancellationToken);

    /// <summary>
    /// Pauses the merchant's live listings beyond <paramref name="quota"/>, most recently
    /// published first stay live. Uses the same merchant-initiated <see cref="Listing.Hide"/>
    /// transition a Pause click uses, not an admin takedown, so the merchant can freely
    /// unpause once they have headroom again.
    /// </summary>
    private async Task<int> PauseListingsBeyondQuotaAsync(
        Guid merchantProfileId, int quota, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var liveListings = await db.Listings
            .Where(l => l.MerchantProfileId == merchantProfileId
                && (l.Status == ListingStatus.Live || l.Status == ListingStatus.SoldOut))
            .OrderByDescending(l => l.PublishedAtUtc)
            .ToListAsync(cancellationToken);

        var toPause = liveListings.Skip(Math.Max(quota, 0)).ToList();
        foreach (var listing in toPause)
        {
            listing.Hide(nowUtc);
        }

        return toPause.Count;
    }

    private async Task HideLiveListingsForLapseAsync(Guid merchantProfileId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var liveListings = await db.Listings
            .Where(l => l.MerchantProfileId == merchantProfileId
                && (l.Status == ListingStatus.Live || l.Status == ListingStatus.SoldOut))
            .ToListAsync(cancellationToken);

        foreach (var listing in liveListings)
        {
            listing.HideForSubscriptionLapse(nowUtc);
        }
    }

    /// <summary>
    /// Restores the listings a lapse hid, most recently published first, up to however much
    /// quota room the plan currently allows — the automatic half of renewal
    /// (<c>BUSINESS-MODEL.md</c> §6.5). Listings a downgrade paused on purpose
    /// (<see cref="PauseListingsBeyondQuotaAsync"/>) are never touched here: they do not carry
    /// <see cref="Listing.HiddenBySubscriptionLapse"/>, so the merchant keeps the choice of
    /// which of those to unpause.
    /// </summary>
    private async Task<int> RestoreListingsHiddenByLapseAsync(
        Guid merchantProfileId, int quota, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var currentlyLive = await CountLiveListingsAsync(merchantProfileId, cancellationToken);
        var slots = quota - currentlyLive;
        if (slots <= 0)
        {
            return 0;
        }

        // WithAggregate() loads Moderations and Variants: RestoreFromSubscriptionLapse needs
        // the former to confirm the listing is still an approved one, and the latter to know
        // whether it comes back Live or SoldOut.
        var lapsedListings = await db.Listings
            .WithAggregate()
            .Where(l => l.MerchantProfileId == merchantProfileId
                && l.Status == ListingStatus.Hidden
                && l.HiddenBySubscriptionLapse)
            .OrderByDescending(l => l.PublishedAtUtc)
            .Take(slots)
            .ToListAsync(cancellationToken);

        foreach (var listing in lapsedListings)
        {
            listing.RestoreFromSubscriptionLapse(nowUtc);
        }

        return lapsedListings.Count;
    }

    /// <summary>
    /// Clears the change tracker between subscriptions in the expiry sweep so one merchant's
    /// tracked graph cannot bleed into the next merchant's save. The runtime type is always
    /// <see cref="Faed.Web.Data.ApplicationDbContext"/>, a <see cref="DbContext"/>.
    /// </summary>
    private void DetachTrackedGraph()
    {
        if (db is DbContext context)
        {
            context.ChangeTracker.Clear();
        }
    }
}
