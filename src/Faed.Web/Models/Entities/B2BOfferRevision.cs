using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// One immutable proposal in a <see cref="B2BNegotiation"/>. Every counter-offer is a new revision with the next
/// <see cref="RevisionNumber"/>; an existing revision is never edited or overwritten.
/// The type exposes no
/// mutators for that reason — it is created once by <see cref="B2BNegotiation"/> and only
/// ever read afterwards.
/// </summary>
public class B2BOfferRevision
{
    public const int MaxMessageLength = 2000;
    public const int JodDecimalPlaces = 3;

    private readonly List<B2BOfferLine> _lines = [];

    private B2BOfferRevision()
    {
    }

    internal B2BOfferRevision(
        int revisionNumber,
        Guid proposedByMerchantProfileId,
        decimal proposedUnitPrice,
        string? message,
        DateTime offerExpiresAtUtc,
        IEnumerable<ProposedOfferLine> lines,
        DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        RevisionNumber = revisionNumber;
        ProposedByMerchantProfileId = proposedByMerchantProfileId;
        ProposedUnitPrice = proposedUnitPrice;
        Message = NormalizeMessage(message);
        OfferExpiresAtUtc = offerExpiresAtUtc;
        CreatedAtUtc = nowUtc;

        foreach (var line in lines)
        {
            _lines.Add(new B2BOfferLine(line.ListingVariantId, line.Quantity));
        }

        // The total is derived from the proposed unit price and the summed line quantities —
        // it is not accepted from input.
        ProposedTotal = proposedUnitPrice * TotalQuantity;
    }

    public Guid Id { get; private set; }

    public Guid B2BNegotiationId { get; private set; }

    /// <summary>Strictly increasing within its negotiation, starting at 1.</summary>
    public int RevisionNumber { get; private set; }

    /// <summary>Which merchant put this proposal forward.</summary>
    public Guid ProposedByMerchantProfileId { get; private set; }

    public decimal ProposedUnitPrice { get; private set; }

    /// <summary>Server-calculated: <see cref="ProposedUnitPrice"/> × <see cref="TotalQuantity"/>.</summary>
    public decimal ProposedTotal { get; private set; }

    public string? Message { get; private set; }

    /// <summary>
    /// When this offer lapses. Distinct from a deal's <c>ReservationExpiresAt</c>:
    /// Once past, the revision can no longer be accepted and the negotiation
    /// expires.
    /// </summary>
    public DateTime OfferExpiresAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<B2BOfferLine> Lines => _lines.AsReadOnly();

    public int TotalQuantity => _lines.Sum(l => l.Quantity);

    public bool HasExpired(DateTime nowUtc) => OfferExpiresAtUtc <= nowUtc;

    internal static bool HasJodPrecision(decimal amount) =>
        decimal.Round(amount, JodDecimalPlaces) == amount;

    private static string? NormalizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var trimmed = message.Trim();
        return trimmed.Length > MaxMessageLength ? trimmed[..MaxMessageLength] : trimmed;
    }
}

/// <summary>
/// A requested quantity of one sellable variant within a <see cref="B2BOfferRevision"/>.
/// Immutable, like the revision that owns it.
/// </summary>
public class B2BOfferLine
{
    private B2BOfferLine()
    {
    }

    internal B2BOfferLine(Guid listingVariantId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("An offer line quantity must be greater than zero.");
        }

        Id = Guid.CreateVersion7();
        ListingVariantId = listingVariantId;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }

    public Guid B2BOfferRevisionId { get; private set; }

    public Guid ListingVariantId { get; private set; }

    public int Quantity { get; private set; }
}
