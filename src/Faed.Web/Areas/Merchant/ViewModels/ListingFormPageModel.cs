using Faed.Web.Services.Listings;

namespace Faed.Web.Areas.Merchant.ViewModels;

/// <summary>
/// Everything the one-page listing form needs: the bound <see cref="ListingFormModel"/>, the
/// DB-driven choices, and — when editing — the saved listing so the page can show its status,
/// existing photos and review history. <see cref="Listing"/> is <c>null</c> when creating.
/// </summary>
public sealed class ListingFormPageModel
{
    public required ListingFormModel Form { get; init; }

    public required ListingReferenceData ReferenceData { get; init; }

    public ListingDetailView? Listing { get; init; }

    public bool IsEdit => Listing is not null;
}
