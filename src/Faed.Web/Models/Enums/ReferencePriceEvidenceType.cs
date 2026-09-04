namespace Faed.Web.Models.Enums;

/// <summary>
/// Provenance of a listing's reference price. A reference
/// price is only meaningful when the merchant can say where the number came from, so the
/// source kind is recorded alongside the URL, uploaded evidence file or note.
/// </summary>
public enum ReferencePriceEvidenceType
{
    /// <summary>The merchant's own current selling price in their normal channel.</summary>
    MerchantCurrentPrice = 0,

    /// <summary>A price the merchant charged in their store before the discount.</summary>
    PreviousStorePrice = 1,

    /// <summary>A supplier or brand catalogue price.</summary>
    CatalogPrice = 2,

    /// <summary>A public product page showing the comparable price.</summary>
    ProductUrl = 3,

    /// <summary>An uploaded invoice, catalogue page or price list.</summary>
    InvoiceOrCatalogDocument = 4,

    /// <summary>A note recorded during admin moderation.</summary>
    AdminNote = 5,
}
