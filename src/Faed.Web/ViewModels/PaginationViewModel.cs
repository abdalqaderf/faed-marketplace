using Faed.Web.Services.Common;

namespace Faed.Web.ViewModels;

/// <summary>
/// View model for the shared <c>_Pagination</c> partial. Carries the totals a pager needs
/// plus the route values (filter, tab, …) that must be preserved across page links.
/// </summary>
public sealed class PaginationViewModel
{
    public required string Action { get; init; }

    public string? Controller { get; init; }

    public required int Page { get; init; }

    public required int TotalPages { get; init; }

    public required int TotalCount { get; init; }

    public required int FirstItemNumber { get; init; }

    public required int LastItemNumber { get; init; }

    /// <summary>Extra route values (e.g. the current filter) to keep on the Previous/Next links.</summary>
    public IReadOnlyDictionary<string, string?> RouteValues { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>Singular noun for the "Showing 1–25 of 90 orders" line.</summary>
    public string ItemNoun { get; init; } = "record";

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    public Dictionary<string, string?> RouteValuesForPage(int page)
    {
        var values = new Dictionary<string, string?>(RouteValues) { ["page"] = page.ToString() };
        return values;
    }

    public static PaginationViewModel From<T>(
        PagedResult<T> result,
        string action,
        IReadOnlyDictionary<string, string?>? routeValues = null,
        string itemNoun = "record",
        string? controller = null) => new()
        {
            Action = action,
            Controller = controller,
            Page = result.Page,
            TotalPages = result.TotalPages,
            TotalCount = result.TotalCount,
            FirstItemNumber = result.FirstItemNumber,
            LastItemNumber = result.LastItemNumber,
            RouteValues = routeValues ?? new Dictionary<string, string?>(),
            ItemNoun = itemNoun,
        };
}
