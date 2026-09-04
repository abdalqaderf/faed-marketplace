using Microsoft.EntityFrameworkCore;

namespace Faed.Web.Services.Common;

/// <summary>
/// Turns an ordered <see cref="IQueryable{T}"/> into a bounded <see cref="PagedResult{T}"/>
/// with a single <c>COUNT</c> and a single windowed row query. Kept separate from the pure
/// <see cref="PagedResult{T}"/> record because it depends on EF Core.
/// </summary>
public static class QueryablePagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Paging.NormalizePage(page);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, page, pageSize);
    }
}
