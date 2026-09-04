namespace Faed.Web.Services.Common;

/// <summary>
/// Shared paging constants. Every list, queue and history surface returns a bounded
/// <see cref="PagedResult{T}"/> so a merchant, buyer or administrator with a long history
/// never triggers an unbounded query.
/// </summary>
public static class Paging
{
    /// <summary>Rows per page for buyer / merchant list and queue surfaces.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Rows per page for admin operational surfaces (review queues and transaction history).</summary>
    public const int AdminPageSize = 50;

    /// <summary>Clamps a 1-based page number to a sane lower bound.</summary>
    public static int NormalizePage(int page) => page < 1 ? 1 : page;
}

/// <summary>
/// A bounded page of a larger ordered result set, plus the totals a pager needs. Page is
/// 1-based.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    /// <summary>1-based index of the first row on this page (0 when the result is empty).</summary>
    public int FirstItemNumber => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    /// <summary>1-based index of the last row on this page (0 when the result is empty).</summary>
    public int LastItemNumber => TotalCount == 0 ? 0 : FirstItemNumber + Items.Count - 1;

    /// <summary>An empty result. There is nothing to page, so it is always page 1 of 1.</summary>
    public static PagedResult<T> Empty(int page, int pageSize) => new([], 0, 1, pageSize);
}
