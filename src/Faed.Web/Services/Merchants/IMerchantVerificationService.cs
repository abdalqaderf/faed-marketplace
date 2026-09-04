using Faed.Web.Services.Common;

namespace Faed.Web.Services.Merchants;

/// <summary>
/// Use cases for merchant application, private document handling and admin review
/// All authorization-sensitive identity is passed explicitly by the caller and
/// re-checked here; nothing is trusted from the browser.
/// </summary>
public interface IMerchantVerificationService
{
    // --- Merchant self-service ---

    Task<MerchantApplicationView?> GetMyApplicationAsync(string userId, CancellationToken cancellationToken = default);

    Task<Result<Guid>> SaveDraftAsync(string userId, MerchantApplicationInput input, CancellationToken cancellationToken = default);

    Task<Result<Guid>> AddDocumentAsync(string userId, AddVerificationDocumentInput input, CancellationToken cancellationToken = default);

    Task<Result> RemoveDocumentAsync(string userId, Guid documentId, CancellationToken cancellationToken = default);

    Task<Result> SubmitForReviewAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>True when the user's merchant profile is Approved. Backs the ApprovedMerchant policy.</summary>
    Task<bool> IsApprovedMerchantAsync(string userId, CancellationToken cancellationToken = default);

    // --- Admin review ---

    Task<PagedResult<MerchantQueueItem>> GetQueueAsync(
        MerchantQueueFilter filter, int page = 1, CancellationToken cancellationToken = default);

    Task<MerchantReviewDetail?> GetForReviewAsync(Guid merchantProfileId, CancellationToken cancellationToken = default);

    Task<Result> ApproveAsync(string adminUserId, Guid merchantProfileId, CancellationToken cancellationToken = default);

    Task<Result> RejectAsync(string adminUserId, Guid merchantProfileId, string reason, CancellationToken cancellationToken = default);

    Task<Result> SuspendAsync(string adminUserId, Guid merchantProfileId, string reason, CancellationToken cancellationToken = default);

    Task<Result> ReinstateAsync(string adminUserId, Guid merchantProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a private verification document to an authorized admin and records the
    /// access in the audit log.
    /// </summary>
    Task<Result<StoredFileContent>> OpenVerificationDocumentAsync(string adminUserId, Guid documentId, CancellationToken cancellationToken = default);
}

/// <summary>Which merchant applications the admin queue should return.</summary>
public enum MerchantQueueFilter
{
    PendingReview = 0,
    All = 1,
    Approved = 2,
    Rejected = 3,
    Suspended = 4,
}
