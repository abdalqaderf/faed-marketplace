namespace Faed.Web.Services.Ordering;

/// <summary>
/// How reliably a merchant answers reservations, computed from order timestamps over a
/// rolling 30-day window (docs/BUSINESS-MODEL.md §8.4). Needs no entity of its own.
/// </summary>
public interface IMerchantResponseService
{
    /// <summary>Response stats for one merchant.</summary>
    Task<MerchantResponseStats> GetAsync(Guid merchantProfileId, CancellationToken cancellationToken = default);

    /// <summary>Response stats for several merchants in one pass (empty input returns an empty map).</summary>
    Task<IReadOnlyDictionary<Guid, MerchantResponseStats>> GetManyAsync(
        IReadOnlyCollection<Guid> merchantProfileIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// The merchant ids that answer slowly enough to rank below responsive merchants in the
    /// catalogue: at least <see cref="MerchantResponseStats.MinimumOrdersForStats"/> orders in
    /// the window and a response rate under <see cref="MerchantResponseService.LowResponderRateThreshold"/>.
    /// A new seller is never in this set — they have not been given the chance to fail.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetLowResponderMerchantIdsAsync(CancellationToken cancellationToken = default);
}
