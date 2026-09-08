using Faed.Web.Services.Common;

namespace Faed.Web.Services.Ordering;

/// <summary>
/// Merchant-managed fulfilment configuration: pickup locations. Ownership is re-resolved from
/// the database on every call — a merchant only ever touches its own rows.
/// </summary>
public interface IMerchantStoreService
{
    Task<MerchantStoreSettingsView> GetSettingsAsync(
        string merchantUserId, CancellationToken cancellationToken = default);

    Task<Result<Guid>> AddLocationAsync(
        string merchantUserId, MerchantLocationInput input, CancellationToken cancellationToken = default);

    Task<Result> UpdateLocationAsync(
        string merchantUserId, Guid locationId, MerchantLocationInput input, CancellationToken cancellationToken = default);

    Task<Result> SetLocationActiveAsync(
        string merchantUserId, Guid locationId, bool isActive, CancellationToken cancellationToken = default);
}
