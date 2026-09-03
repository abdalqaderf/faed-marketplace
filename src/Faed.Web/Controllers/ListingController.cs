using Faed.Web.Services.Marketplace;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Controllers;

/// <summary>
/// The public listing detail page (tasks/TASK-005-PUBLIC-MARKETPLACE.md). Resolved by slug,
/// never by database id (docs/06-ARCHITECTURE.md §12); anything that is not a Live listing —
/// including a valid slug for a Draft, Hidden or SoldOut listing — is a 404, never a partial
/// render (docs/11-ACCEPTANCE-CRITERIA.md "Public sees only Live listings").
/// </summary>
[Route("listing")]
public sealed class ListingController(IPublicMarketplaceService marketplace) : Controller
{
    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var listing = await marketplace.GetListingBySlugAsync(slug, cancellationToken);
        return listing is null ? NotFound() : View(listing);
    }
}
