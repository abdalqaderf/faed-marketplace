using Faed.Web.Models.Entities;
using Faed.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Faed.Web.Services.Ordering;

/// <inheritdoc />
public sealed class MerchantResponseService(
    IApplicationDbContext db, IClock clock, IOptions<OrderingOptions> options) : IMerchantResponseService
{
    /// <summary>The rolling window the rate and time are measured over.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(30);

    /// <summary>A merchant answering fewer than half of their reservations in time ranks lower.</summary>
    public const double LowResponderRateThreshold = 0.5;

    private readonly OrderingOptions _options = options.Value;

    public async Task<MerchantResponseStats> GetAsync(Guid merchantProfileId, CancellationToken cancellationToken = default)
    {
        var map = await GetManyAsync([merchantProfileId], cancellationToken);
        return map.GetValueOrDefault(merchantProfileId, MerchantResponseStats.NewSeller);
    }

    public async Task<IReadOnlyDictionary<Guid, MerchantResponseStats>> GetManyAsync(
        IReadOnlyCollection<Guid> merchantProfileIds, CancellationToken cancellationToken = default)
    {
        if (merchantProfileIds.Count == 0)
        {
            return new Dictionary<Guid, MerchantResponseStats>();
        }

        var since = clock.UtcNow - Window;
        var deadline = _options.ReservationWindow;

        // The window bounds the row count (a merchant's last 30 days of orders), so materialising
        // and folding in memory is simpler than a database median and fast enough until a real
        // bottleneck is measured.
        var rows = await db.Orders
            .AsNoTracking()
            .Where(o => merchantProfileIds.Contains(o.MerchantProfileId) && o.CreatedAtUtc >= since)
            .Select(o => new { o.MerchantProfileId, o.CreatedAtUtc, o.ConfirmedAtUtc })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.MerchantProfileId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var received = g.Count();
                    var inTime = g
                        .Where(r => r.ConfirmedAtUtc is { } confirmed && confirmed - r.CreatedAtUtc <= deadline)
                        .Select(r => r.ConfirmedAtUtc!.Value - r.CreatedAtUtc)
                        .ToList();

                    ResponseTimeBucket? median = inTime.Count == 0 ? null : Bucket(Median(inTime));
                    return new MerchantResponseStats(received, inTime.Count, median);
                });
    }

    public async Task<IReadOnlySet<Guid>> GetLowResponderMerchantIdsAsync(CancellationToken cancellationToken = default)
    {
        var since = clock.UtcNow - Window;
        var deadline = _options.ReservationWindow;

        var rows = await db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAtUtc >= since)
            .Select(o => new { o.MerchantProfileId, o.CreatedAtUtc, o.ConfirmedAtUtc })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.MerchantProfileId)
            .Where(g =>
            {
                var received = g.Count();
                if (received < MerchantResponseStats.MinimumOrdersForStats)
                {
                    return false;
                }

                var inTime = g.Count(r => r.ConfirmedAtUtc is { } confirmed && confirmed - r.CreatedAtUtc <= deadline);
                return (double)inTime / received < LowResponderRateThreshold;
            })
            .Select(g => g.Key)
            .ToHashSet();
    }

    private static TimeSpan Median(List<TimeSpan> values)
    {
        values.Sort();
        var mid = values.Count / 2;
        return values.Count % 2 == 1
            ? values[mid]
            : TimeSpan.FromTicks((values[mid - 1].Ticks + values[mid].Ticks) / 2);
    }

    private static ResponseTimeBucket Bucket(TimeSpan median) => median switch
    {
        { TotalHours: <= 1 } => ResponseTimeBucket.WithinHour,
        { TotalHours: <= 2 } => ResponseTimeBucket.WithinTwoHours,
        { TotalHours: <= 24 } => ResponseTimeBucket.WithinDay,
        _ => ResponseTimeBucket.MoreThanDay,
    };
}
