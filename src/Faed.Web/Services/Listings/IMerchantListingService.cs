using Faed.Web.Services.Common;

namespace Faed.Web.Services.Listings;


/// <summary>
/// Merchant-side listing use cases.
/// Every method takes the acting Identity user id and re-resolves the owning merchant from
/// the database. Ownership is never inferred from a route value, a form field or a hidden
/// input, and only an Approved merchant can reach
/// any of it.
/// </summary>
public interface IMerchantListingService
{
    /// <summary>DB-driven choices for the listing form (categories, grades, reasons, brands).</summary>
    Task<ListingReferenceData> GetReferenceDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// A bounded, most-recently-updated-first page of the caller's own listings — a
    /// merchant's listing count grows without limit over time, so this is a real page rather
    /// than the full set.
    /// </summary>
    Task<PagedResult<MerchantListingListItem>> GetMyListingsAsync(
        string userId, MerchantListingFilter filter, int page = 1, CancellationToken cancellationToken = default);

    /// <summary>The merchant's own listing, or <c>null</c> when it is not theirs.</summary>
    Task<ListingDetailView?> GetMyListingAsync(
        string userId, Guid listingId, CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreateAsync(
        string userId, ListingDetailsInput input, CancellationToken cancellationToken = default);

    Task<Result> UpdateDetailsAsync(
        string userId, Guid listingId, ListingDetailsInput input, CancellationToken cancellationToken = default);

    Task<Result> AddOptionAsync(
        string userId, Guid listingId, string name, CancellationToken cancellationToken = default);

    Task<Result> RemoveOptionAsync(
        string userId, Guid listingId, Guid optionId, CancellationToken cancellationToken = default);

    Task<Result> AddOptionValueAsync(
        string userId, Guid listingId, Guid optionId, string value, CancellationToken cancellationToken = default);

    Task<Result> RemoveOptionValueAsync(
        string userId, Guid listingId, Guid optionId, Guid optionValueId, CancellationToken cancellationToken = default);

    Task<Result> AddVariantAsync(
        string userId, Guid listingId, AddVariantInput input, CancellationToken cancellationToken = default);

    Task<Result> RemoveVariantAsync(
        string userId, Guid listingId, Guid variantId, CancellationToken cancellationToken = default);

    Task<Result> SetVariantActiveAsync(
        string userId, Guid listingId, Guid variantId, bool isActive, CancellationToken cancellationToken = default);

    Task<Result> AddImageAsync(
        string userId, Guid listingId, AddListingImageInput input, CancellationToken cancellationToken = default);

    Task<Result> RemoveImageAsync(
        string userId, Guid listingId, Guid mediaId, CancellationToken cancellationToken = default);

    Task<Result> AddReferencePriceEvidenceAsync(
        string userId, Guid listingId, AddReferencePriceEvidenceInput input, CancellationToken cancellationToken = default);

    Task<Result> RemoveReferencePriceEvidenceAsync(
        string userId, Guid listingId, Guid evidenceId, CancellationToken cancellationToken = default);

    Task<Result> SubmitForReviewAsync(
        string userId, Guid listingId, CancellationToken cancellationToken = default);

    Task<Result> HideAsync(string userId, Guid listingId, CancellationToken cancellationToken = default);

    Task<Result> RestoreAsync(string userId, Guid listingId, CancellationToken cancellationToken = default);

    Task<Result> ArchiveAsync(string userId, Guid listingId, CancellationToken cancellationToken = default);
}
