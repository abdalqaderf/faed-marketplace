namespace Faed.Web.Services.Analytics;

/// <summary>
/// Merchant recovery analytics. Every value is recomputed from authoritative
/// order / deal / listing data on each request; nothing is stored or trusted from the client
/// </summary>
public interface IMerchantAnalyticsService
{
    /// <summary>
    /// The signed-in merchant's analytics. Returns an all-zero view (never null) when the
    /// user has no merchant profile yet, so the page renders an empty state rather than 404.
    /// </summary>
    Task<MerchantAnalyticsView> GetForOwnerAsync(string merchantUserId, CancellationToken cancellationToken = default);
}
