namespace Faed.Web.Models.Enums;

/// <summary>
/// Provenance of a listing's reference price. A reference
/// price is only meaningful when the merchant can say where the number came from, so the
/// source kind is recorded alongside the URL or uploaded photo.
/// </summary>
public enum ReferencePriceEvidenceType
{
    /// <summary>An uploaded photo showing the original price — a shelf tag, receipt or box label.</summary>
    Photo = 0,

    /// <summary>A public product page showing the comparable price.</summary>
    Link = 1,
}
