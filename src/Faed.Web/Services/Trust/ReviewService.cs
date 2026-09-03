using Faed.Web.Authorization;
using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faed.Web.Services.Trust;

/// <inheritdoc />
public sealed class ReviewService(
    IApplicationDbContext db,
    IUserRoleService userRoles,
    IClock clock,
    ILogger<ReviewService> logger) : IReviewService
{
    public async Task<Result<Guid>> SubmitReviewAsync(
        string userId, SubmitReviewInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<Guid>.Forbidden("Sign in to leave a review.");
        }

        // A review is left by the buyer, never by an administrator
        // (docs/16-PERMISSIONS-MATRIX.md "Leave review — Admin ❌").
        if (await userRoles.IsInRoleAsync(userId, FaedRoles.Admin, cancellationToken))
        {
            return Result<Guid>.Forbidden("Administrators cannot leave reviews.");
        }

        if (input.Rating is < Review.MinRating or > Review.MaxRating)
        {
            return Result<Guid>.Validation($"Choose a rating from {Review.MinRating} to {Review.MaxRating}.");
        }

        var comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
        if (comment is { Length: > Review.MaxCommentLength })
        {
            return Result<Guid>.Validation($"A comment must be {Review.MaxCommentLength} characters or fewer.");
        }

        var eligibility = await ResolveEligibilityAsync(userId, input.TransactionType, input.TransactionId, cancellationToken);
        if (eligibility.Merchant is null)
        {
            return Result<Guid>.NotFound("That transaction was not found.");
        }

        if (!eligibility.IsParticipant)
        {
            // A non-participant learns nothing (docs/08-SECURITY-AND-PRIVACY.md §9).
            return Result<Guid>.NotFound("That transaction was not found.");
        }

        if (!eligibility.IsCompleted)
        {
            return Result<Guid>.Validation("You can review a merchant only after the transaction is completed.");
        }

        if (eligibility.AlreadyReviewed)
        {
            return Result<Guid>.Conflict("You have already reviewed this transaction.");
        }

        var review = new Review(
            eligibility.Merchant.Value,
            userId,
            input.TransactionType == TrustTransactionType.B2COrder ? input.TransactionId : null,
            input.TransactionType == TrustTransactionType.B2BDeal ? input.TransactionId : null,
            input.Rating,
            comment,
            clock.UtcNow);

        db.Reviews.Add(review);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // The filtered unique index on the transaction FK is the backstop for the
            // duplicate-review rule (docs/03-BUSINESS-RULES.md §13). Two submissions racing
            // each other both pass the pre-check; the loser lands here.
            logger.LogInformation(ex, "Duplicate review rejected for user {UserId} on {Type} {TransactionId}",
                userId, input.TransactionType, input.TransactionId);
            return Result<Guid>.Conflict("You have already reviewed this transaction.");
        }

        logger.LogInformation("User {UserId} reviewed merchant {MerchantId} ({Rating}/5) for {Type} {TransactionId}",
            userId, review.ReviewedMerchantProfileId, review.Rating, input.TransactionType, input.TransactionId);
        return Result<Guid>.Success(review.Id);
    }

    public async Task<ReviewEligibilityView> GetEligibilityAsync(
        string userId, TrustTransactionType transactionType, Guid transactionId, CancellationToken cancellationToken = default)
    {
        var eligibility = await ResolveEligibilityAsync(userId, transactionType, transactionId, cancellationToken);

        if (eligibility.Merchant is null || !eligibility.IsParticipant)
        {
            return new ReviewEligibilityView(false, false, null, null);
        }

        if (eligibility.ExistingReview is { } existing)
        {
            return new ReviewEligibilityView(
                false, true,
                new ExistingReviewView(existing.Rating, existing.Comment, existing.CreatedAtUtc),
                null);
        }

        if (!eligibility.IsCompleted)
        {
            return new ReviewEligibilityView(
                false, false, null,
                "You can review this merchant once the transaction is completed.");
        }

        return new ReviewEligibilityView(true, false, null, null);
    }

    public async Task<MerchantReviewsView> GetMerchantReviewsAsync(
        Guid merchantProfileId, int take, CancellationToken cancellationToken = default)
    {
        var stats = await db.Reviews
            .AsNoTracking()
            .Where(r => r.ReviewedMerchantProfileId == merchantProfileId)
            .GroupBy(r => 1)
            .Select(g => new { Count = g.Count(), Average = g.Average(r => (double)r.Rating) })
            .SingleOrDefaultAsync(cancellationToken);

        var summary = stats is null
            ? new MerchantRatingSummary(0, 0)
            : new MerchantRatingSummary(stats.Count, Math.Round(stats.Average, 2));

        var recent = await db.Reviews
            .AsNoTracking()
            .Where(r => r.ReviewedMerchantProfileId == merchantProfileId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 50))
            .Select(r => new
            {
                r.Rating,
                r.Comment,
                Type = r.OrderId != null ? TrustTransactionType.B2COrder : TrustTransactionType.B2BDeal,
                r.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var reviews = recent
            .Select(r => new MerchantReviewView(
                r.Rating, r.Comment, r.Type,
                r.Type == TrustTransactionType.B2COrder ? "Individual buyer" : "Wholesale buyer",
                r.CreatedAtUtc))
            .ToList();

        return new MerchantReviewsView(summary, reviews);
    }

    public async Task<MerchantReviewsView> GetReviewsForOwnerAsync(
        string merchantUserId, CancellationToken cancellationToken = default)
    {
        var merchantId = await db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == merchantUserId)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return merchantId is null
            ? new MerchantReviewsView(new MerchantRatingSummary(0, 0), [])
            : await GetMerchantReviewsAsync(merchantId.Value, 50, cancellationToken);
    }

    // ---- Internals ---------------------------------------------------------

    private async Task<EligibilitySnapshot> ResolveEligibilityAsync(
        string userId, TrustTransactionType type, Guid transactionId, CancellationToken cancellationToken)
    {
        if (type == TrustTransactionType.B2COrder)
        {
            var order = await db.Orders
                .AsNoTracking()
                .Where(o => o.Id == transactionId)
                .Select(o => new { o.MerchantProfileId, o.BuyerUserId, o.Status })
                .SingleOrDefaultAsync(cancellationToken);
            if (order is null)
            {
                return EligibilitySnapshot.None;
            }

            var existing = await db.Reviews.AsNoTracking()
                .SingleOrDefaultAsync(r => r.OrderId == transactionId, cancellationToken);

            return new EligibilitySnapshot
            {
                Merchant = order.MerchantProfileId,
                IsParticipant = order.BuyerUserId == userId,
                IsCompleted = order.Status == OrderStatus.Completed,
                ExistingReview = existing,
            };
        }

        var deal = await db.B2BDeals
            .AsNoTracking()
            .Where(d => d.Id == transactionId)
            .Select(d => new { d.SellingMerchantProfileId, d.BuyingMerchantProfileId, d.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (deal is null)
        {
            return EligibilitySnapshot.None;
        }

        // Only the buying merchant reviews the selling merchant for a wholesale deal
        // (docs/03-BUSINESS-RULES.md §13 "reviewer participated in the transaction").
        var buyingMerchantUserId = await db.MerchantProfiles
            .AsNoTracking()
            .Where(m => m.Id == deal.BuyingMerchantProfileId)
            .Select(m => m.UserId)
            .SingleOrDefaultAsync(cancellationToken);

        var existingDealReview = await db.Reviews.AsNoTracking()
            .SingleOrDefaultAsync(r => r.B2BDealId == transactionId, cancellationToken);

        return new EligibilitySnapshot
        {
            Merchant = deal.SellingMerchantProfileId,
            IsParticipant = buyingMerchantUserId == userId,
            IsCompleted = deal.Status == B2BDealStatus.Completed,
            ExistingReview = existingDealReview,
        };
    }

    private sealed class EligibilitySnapshot
    {
        public static readonly EligibilitySnapshot None = new();

        public Guid? Merchant { get; init; }

        public bool IsParticipant { get; init; }

        public bool IsCompleted { get; init; }

        public Review? ExistingReview { get; init; }

        public bool AlreadyReviewed => ExistingReview is not null;
    }
}
