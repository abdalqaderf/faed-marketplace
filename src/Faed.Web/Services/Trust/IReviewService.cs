using Faed.Web.Models.Enums;
using Faed.Web.Services.Common;

namespace Faed.Web.Services.Trust;

/// <summary>
/// Merchant reviews left after a completed transaction (docs/03-BUSINESS-RULES.md §13,
/// docs/10-IMPLEMENTATION-PLAN.md Phase 8). A review is allowed only when the transaction is
/// <c>Completed</c>, the reviewer took part, and they have not already reviewed it; the
/// duplicate rule is also a database unique constraint.
/// </summary>
public interface IReviewService
{
    Task<Result<Guid>> SubmitReviewAsync(
        string userId, SubmitReviewInput input, CancellationToken cancellationToken = default);

    Task<ReviewEligibilityView> GetEligibilityAsync(
        string userId, TrustTransactionType transactionType, Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>Public rating summary and recent reviews for a merchant storefront.</summary>
    Task<MerchantReviewsView> GetMerchantReviewsAsync(
        Guid merchantProfileId, int take, CancellationToken cancellationToken = default);

    /// <summary>The reviews a signed-in merchant has received, for their own dashboard.</summary>
    Task<MerchantReviewsView> GetReviewsForOwnerAsync(
        string merchantUserId, CancellationToken cancellationToken = default);
}
