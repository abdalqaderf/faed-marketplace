using Faed.Web.Services.Common;

namespace Faed.Web.Services.Merchants;

/// <summary>
/// Generates human-readable public storefront slugs. Slugs are display identifiers only,
/// never authorization keys.
/// </summary>
public static class MerchantSlug
{
    public static string Slugify(string value) => Slug.Create(value, "merchant");
}
