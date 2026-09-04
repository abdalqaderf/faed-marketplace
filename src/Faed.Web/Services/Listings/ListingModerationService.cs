using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faed.Web.Services.Listings;

/// <inheritdoc />
public sealed class ListingModerationService(
    IApplicationDbContext db,
    IUserRoleService userRoles,
    IClock clock,
    ILogger<ListingModerationService> logger) : IListingModerationService
{
    private const string ListingTargetType = nameof(Listing);

    public Task<PagedResult<ModerationQueueItem>> GetQueueAsync(
        ModerationQueueFilter filter, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = db.Listings.AsNoTracking();

        query = filter switch
        {
            ModerationQueueFilter.PendingReview => query.Where(l => l.Status == ListingStatus.PendingReview),
            ModerationQueueFilter.Live => query.Where(l =>
                l.Status == ListingStatus.Live || l.Status == ListingStatus.SoldOut),
            ModerationQueueFilter.Rejected => query.Where(l => l.Status == ListingStatus.Rejected),
            _ => query.Where(l => l.Status != ListingStatus.Draft && l.Status != ListingStatus.Archived),
        };

        return query
            // Oldest submission first: the queue is a work list, not a news feed.
            .OrderBy(l => l.Status == ListingStatus.PendingReview ? 0 : 1)
            .ThenBy(l => l.SubmittedAtUtc ?? l.CreatedAtUtc)
            .Select(l => new ModerationQueueItem(
                l.Id,
                l.Title,
                db.MerchantProfiles.Where(m => m.Id == l.MerchantProfileId)
                    .Select(m => m.BusinessName).FirstOrDefault() ?? "Unknown merchant",
                l.Status,
                l.Moderations
                    .OrderByDescending(m => m.SubmittedAtUtc)
                    .Select(m => m.ReasonForReview)
                    .FirstOrDefault() ?? "—",
                l.SubmittedAtUtc ?? l.CreatedAtUtc,
                l.Variants.Count,
                l.Media.Count(m => m.MediaType == ListingMediaType.Defect),
                l.RetailPrice,
                l.ReferencePrice,
                l.ReferencePriceEvidence.Any()))
            .ToPagedResultAsync(page, Paging.AdminPageSize, cancellationToken);
    }

    public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default) =>
        db.Listings.AsNoTracking().CountAsync(l => l.Status == ListingStatus.PendingReview, cancellationToken);

    public async Task<ListingDetailView?> GetForModerationAsync(
        Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await db.Listings
            .AsNoTracking()
            .WithAggregate()
            .SingleOrDefaultAsync(l => l.Id == listingId, cancellationToken);

        return listing is null ? null : await listing.ToDetailViewAsync(db, cancellationToken);
    }

    public Task<Result> ApproveAsync(
        string adminUserId, Guid listingId, string? reviewNote, CancellationToken cancellationToken = default) =>
        DecideAsync(
            adminUserId,
            listingId,
            (listing, now) => listing.Approve(adminUserId, reviewNote, now),
            AdminActionType.ListingApproved,
            reviewNote,
            cancellationToken);

    public Task<Result> RejectAsync(
        string adminUserId, Guid listingId, string reason, CancellationToken cancellationToken = default)
    {
        if (ValidateReason(reason, "rejection") is { Failed: true } invalid)
        {
            return Task.FromResult(invalid);
        }

        return DecideAsync(
            adminUserId,
            listingId,
            (listing, now) => listing.Reject(adminUserId, reason, now),
            AdminActionType.ListingRejected,
            reason.Trim(),
            cancellationToken);
    }

    public Task<Result> HideAsync(
        string adminUserId, Guid listingId, string reason, CancellationToken cancellationToken = default)
    {
        if (ValidateReason(reason, "hide") is { Failed: true } invalid)
        {
            return Task.FromResult(invalid);
        }

        return DecideAsync(
            adminUserId,
            listingId,
            (listing, now) => listing.HideByAdmin(adminUserId, reason, now),
            AdminActionType.ListingHidden,
            reason.Trim(),
            cancellationToken);
    }

    public Task<Result> RestoreAsync(
        string adminUserId, Guid listingId, CancellationToken cancellationToken = default) =>
        DecideAsync(
            adminUserId,
            listingId,
            (listing, now) => listing.RestoreByAdmin(adminUserId, now),
            AdminActionType.ListingRestored,
            notes: null,
            cancellationToken);

    private async Task<Result> DecideAsync(
        string adminUserId,
        Guid listingId,
        Action<Listing, DateTime> transition,
        AdminActionType actionType,
        string? notes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adminUserId)
            || !await userRoles.IsInRoleAsync(adminUserId, FaedRoles.Admin, cancellationToken))
        {
            // The MVC route is already behind the AdminOnly policy; the service contract still
            // does not trust its caller (docs/08-SECURITY-AND-PRIVACY.md §2).
            return Result.Forbidden();
        }

        var listing = await db.Listings
            .WithAggregate()
            .SingleOrDefaultAsync(l => l.Id == listingId, cancellationToken);

        if (listing is null)
        {
            return Result.NotFound("That listing was not found.");
        }

        if (actionType == AdminActionType.ListingApproved)
        {
            // "A Live Listing's merchant must be approved" (docs/17-DATA-INVARIANTS.md) is the
            // natural enforcement point here: a merchant can be suspended or rejected between
            // submission and this decision, and publishing must not silently ignore that
            // (AGENTS.md §3 — a suspended merchant cannot act as seller).
            var merchantIsApproved = await db.MerchantProfiles
                .AsNoTracking()
                .AnyAsync(
                    p => p.Id == listing.MerchantProfileId && p.VerificationStatus == MerchantVerificationStatus.Approved,
                    cancellationToken);

            if (!merchantIsApproved)
            {
                return Result.Conflict(
                    "This listing's merchant is no longer an approved seller, so it cannot be published.");
            }

            // Approval publishes whatever the listing currently is, not the snapshot that was
            // submitted. A blocker can reappear after submission — most importantly the merchant
            // removing the last defect/packaging photo a disclosed imperfection depends on — so
            // the submission checks run once more here rather than trusting that nothing changed
            // (docs/03-BUSINESS-RULES.md §3, docs/17-DATA-INVARIANTS.md "Listing").
            var (conditionGradeCode, discountReasonCodes) =
                await listing.LoadDisclosureCodesAsync(db, cancellationToken);
            var blockers = listing.DescribeSubmissionBlockers(conditionGradeCode, discountReasonCodes);
            if (blockers.Count > 0)
            {
                return Result.Conflict(
                    $"This listing no longer meets the requirements for publication: {blockers[0]}");
            }
        }

        try
        {
            transition(listing, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Conflict(ex.Message);
        }

        db.AdminActionLogs.Add(new AdminActionLog(
            adminUserId,
            actionType,
            ListingTargetType,
            listing.Id.ToString(),
            notes,
            clock.UtcNow));

        // A moderation outcome without its audit entry would be unauditable, so the decision
        // and the log commit together (docs/08-SECURITY-AND-PRIVACY.md §13).
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict(
                "This listing was updated by someone else. Reload it and try again.");
        }

        logger.LogInformation(
            "Admin {AdminId} performed {Action} on listing {ListingId}", adminUserId, actionType, listing.Id);
        return Result.Success();
    }

    private static Result ValidateReason(string? reason, string kind)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Validation($"A {kind} reason is required.");
        }

        return reason.Trim().Length > Listing.MaxDecisionReasonLength
            ? Result.Validation($"The {kind} reason must be {Listing.MaxDecisionReasonLength} characters or fewer.")
            : Result.Success();
    }
}
