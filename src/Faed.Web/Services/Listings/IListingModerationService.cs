using Faed.Web.Services.Common;
using Faed.Web.Services.Merchants;

namespace Faed.Web.Services.Listings;

/// <summary>
/// Admin listing moderation. The reviewing admin is re-checked against the Identity role
/// inside every method: the MVC route is already behind the AdminOnly policy, but the service
/// contract does not trust its caller.
/// Decisions are written with their <c>AdminActionLog</c> entry in one transaction, so a
/// moderation outcome always has an audit trail.
/// </summary>
public interface IListingModerationService
{
    Task<PagedResult<ModerationQueueItem>> GetQueueAsync(
        ModerationQueueFilter filter, int page = 1, CancellationToken cancellationToken = default);

    /// <summary>The number of listings currently awaiting review — a cheap count, not the full queue.</summary>
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Full listing detail for review, including everything the merchant submitted.</summary>
    Task<ListingDetailView?> GetForModerationAsync(Guid listingId, CancellationToken cancellationToken = default);

    Task<Result> ApproveAsync(
        string adminUserId, Guid listingId, string? reviewNote, CancellationToken cancellationToken = default);

    Task<Result> RejectAsync(
        string adminUserId, Guid listingId, string reason, CancellationToken cancellationToken = default);

    Task<Result> HideAsync(
        string adminUserId, Guid listingId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lifts an admin takedown (or a merchant's own hide, on the admin's behalf) and
    /// republishes the listing. The only way to reverse <see cref="HideAsync"/> — a merchant
    /// cannot restore a listing an admin hid.
    /// </summary>
    Task<Result> RestoreAsync(
        string adminUserId, Guid listingId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Serves listing photography from private object storage. There is no public URL to the
/// bytes: every request is resolved through this service so a non-Live listing's images
/// cannot be reached by guessing a path.
/// </summary>
public interface IListingMediaService
{
    /// <summary>
    /// Opens one listing image for a caller entitled to see it: anyone for a Live listing,
    /// and additionally the owning merchant or an admin for one that is not (yet, or no
    /// longer) public — including SoldOut, which is addressable to authorized users only,
    /// not to anonymous traffic. <paramref name="userId"/> is
    /// <c>null</c> for an anonymous request.
    /// </summary>
    Task<Result<StoredFileContent>> OpenImageAsync(
        string? userId, Guid mediaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens one reference-price evidence file. Unlike listing photography this is never
    /// public: only the owning merchant and an admin have any reason to see a supplier
    /// invoice or catalogue scan.
    /// </summary>
    Task<Result<StoredFileContent>> OpenReferencePriceEvidenceAsync(
        string? userId, Guid evidenceId, CancellationToken cancellationToken = default);
}
