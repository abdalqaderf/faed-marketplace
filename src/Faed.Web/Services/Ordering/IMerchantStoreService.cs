using Faed.Web.Services.Common;

namespace Faed.Web.Services.Ordering;

/// <summary>
/// Merchant-managed fulfilment configuration: pickup locations and delivery zones
/// (docs/03-BUSINESS-RULES.md §12, tasks/TASK-006-B2C-ORDERS.md "Pickup", "MerchantDelivery").
/// Ownership is re-resolved from the database on every call — a merchant only ever touches
/// its own rows (docs/03-BUSINESS-RULES.md §16).
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

    Task<Result<Guid>> AddDeliveryZoneAsync(
        string merchantUserId, MerchantDeliveryZoneInput input, CancellationToken cancellationToken = default);

    Task<Result> UpdateDeliveryZoneAsync(
        string merchantUserId, Guid zoneId, MerchantDeliveryZoneInput input, CancellationToken cancellationToken = default);

    Task<Result> SetDeliveryZoneActiveAsync(
        string merchantUserId, Guid zoneId, bool isActive, CancellationToken cancellationToken = default);
}
