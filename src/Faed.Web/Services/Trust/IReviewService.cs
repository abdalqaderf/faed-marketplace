using Faed.Web.Services.Common;

namespace Faed.Web.Services.Trust;

/// <summary>
/// Merchant reviews left after a completed order. A review is allowed only when the order is
/// <c>Completed</c>, the reviewer took part, and they have not already reviewed it; the
/// duplicate rule is also a database unique constraint.
/// </summary>
public interface IReviewService
{
    Task<Result<Guid>> SubmitReviewAsync(
        string userId, SubmitReviewInput input, CancellationToken cancellationToken = default);

    Task<ReviewEligibilityView> GetEligibilityAsync(
        string userId, Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Public rating summary and recent reviews for a merchant storefront.</summary>
    Task<MerchantReviewsView> GetMerchantReviewsAsync(
        Guid merchantProfileId, int take, CancellationToken cancellationToken = default);

    /// <summary>The reviews a signed-in merchant has received, for their own dashboard.</summary>
    Task<MerchantReviewHistoryView> GetReviewsForOwnerAsync(
        string merchantUserId, int page = 1, CancellationToken cancellationToken = default);
}
