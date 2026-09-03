using Faed.Web.Models;
using Faed.Web.Models.Enums;

namespace Faed.Web.Models.Entities;

/// <summary>
/// A structured wholesale negotiation between two verified merchants over one listing
/// (docs/03-BUSINESS-RULES.md §9, docs/04-DOMAIN-MODEL.md §7,
/// docs/adr/0004-B2B-NEGOTIATION-SEPARATE-FROM-DEAL.md). It owns an append-only history of
/// immutable <see cref="B2BOfferRevision"/>s: a counter-offer is a brand-new revision, never
/// an edit of the previous one (AGENTS.md Rule C — "B2B negotiation is not the accepted
/// deal").
///
/// This aggregate models the <em>negotiation</em> only. Accepting the current revision moves
/// it to <see cref="B2BNegotiationStatus.Accepted"/> and records which revision both sides
/// agreed on; it reserves no stock and creates no fulfillment record. The atomic stock
/// reservation and the <c>B2BDeal</c> are TASK-008 (tasks/TASK-007-B2B-NEGOTIATION.md
/// "No stock is permanently consumed by negotiation alone").
/// </summary>
public class B2BNegotiation
{
    private readonly List<B2BOfferRevision> _revisions = [];

    private B2BNegotiation()
    {
    }

    /// <summary>
    /// Opens a negotiation with the buying merchant's first offer (revision 1). The first
    /// revision is always proposed by the buying merchant — a negotiation starts when a
    /// merchant makes an offer on another merchant's listing
    /// (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §5).
    /// </summary>
    /// <param name="listingMinimumOrderQuantity">The listing's <see cref="Listing.WholesaleMinQuantity"/> (docs/03-BUSINESS-RULES.md §11).</param>
    /// <param name="listingAllowsMixedVariantLots">The listing's <see cref="Listing.AllowMixedVariantB2B"/>.</param>
    public B2BNegotiation(
        Guid listingId,
        Guid sellingMerchantProfileId,
        Guid buyingMerchantProfileId,
        int listingMinimumOrderQuantity,
        bool listingAllowsMixedVariantLots,
        ProposedOffer offer,
        DateTime nowUtc)
    {
        if (sellingMerchantProfileId == buyingMerchantProfileId)
        {
            // AGENTS.md §3 (individuals/merchants cannot buy from themselves),
            // docs/17-DATA-INVARIANTS.md "Selling and buying merchants cannot be the same merchant".
            throw new DomainException("A merchant cannot open a wholesale negotiation on its own listing.");
        }

        Id = Guid.CreateVersion7();
        ListingId = listingId;
        SellingMerchantProfileId = sellingMerchantProfileId;
        BuyingMerchantProfileId = buyingMerchantProfileId;
        Status = B2BNegotiationStatus.Open;
        CurrentRevisionNumber = 0;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        AppendRevision(buyingMerchantProfileId, listingMinimumOrderQuantity, listingAllowsMixedVariantLots, offer, nowUtc);
    }

    public Guid Id { get; private set; }

    public Guid ListingId { get; private set; }

    /// <summary>The listing owner. Responds to offers the buying merchant makes.</summary>
    public Guid SellingMerchantProfileId { get; private set; }

    /// <summary>The merchant who opened the negotiation by making the first offer.</summary>
    public Guid BuyingMerchantProfileId { get; private set; }

    public B2BNegotiationStatus Status { get; private set; }

    /// <summary>The revision number of the offer currently on the table (0 only during construction).</summary>
    public int CurrentRevisionNumber { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Guards a buyer and a seller acting on the same negotiation at the same time.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>
    /// The full, append-only offer/counter-offer history. Revisions are only ever appended,
    /// so the backing order is the revision-number order (oldest first). Never mutated.
    /// </summary>
    public IReadOnlyCollection<B2BOfferRevision> Revisions => _revisions.AsReadOnly();

    /// <summary>The offer currently on the table.</summary>
    public B2BOfferRevision CurrentRevision =>
        _revisions.OrderByDescending(r => r.RevisionNumber).First();

    public bool IsOpen => Status == B2BNegotiationStatus.Open;

    public bool IsParticipant(Guid merchantProfileId) =>
        merchantProfileId == SellingMerchantProfileId || merchantProfileId == BuyingMerchantProfileId;

    /// <summary>The other merchant in the negotiation.</summary>
    public Guid CounterpartyOf(Guid merchantProfileId) =>
        merchantProfileId == SellingMerchantProfileId ? BuyingMerchantProfileId : SellingMerchantProfileId;

    /// <summary>
    /// The merchant whose turn it is to accept, reject or counter — the one who did
    /// <em>not</em> propose the current revision. <c>null</c> once the negotiation is closed.
    /// </summary>
    public Guid? AwaitingResponseFrom =>
        IsOpen ? CounterpartyOf(CurrentRevision.ProposedByMerchantProfileId) : null;

    /// <summary>True when <paramref name="merchantProfileId"/> can currently accept/reject/counter.</summary>
    public bool CanBeRespondedToBy(Guid merchantProfileId) =>
        IsOpen && AwaitingResponseFrom == merchantProfileId;

    public bool CurrentOfferHasExpired(DateTime nowUtc) => CurrentRevision.OfferExpiresAtUtc <= nowUtc;

    // ---- Commands -----------------------------------------------------------------

    /// <summary>
    /// Records a counter-offer as a new immutable revision. The proposer must be the merchant
    /// whose turn it is (the one who did not make the current offer) — a merchant cannot
    /// counter its own offer, and the sides strictly alternate
    /// (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §5).
    /// </summary>
    public B2BOfferRevision Counter(
        Guid proposingMerchantProfileId,
        int listingMinimumOrderQuantity,
        bool listingAllowsMixedVariantLots,
        ProposedOffer offer,
        DateTime nowUtc)
    {
        RequireOpen("countered");
        RequireResponder(proposingMerchantProfileId, "counter this offer");
        RequireCurrentOfferActive(nowUtc, "countered");

        return AppendRevision(
            proposingMerchantProfileId, listingMinimumOrderQuantity, listingAllowsMixedVariantLots, offer, nowUtc);
    }

    /// <summary>
    /// Accepts the current offer revision. Only the merchant it is addressed to can accept it,
    /// and only while it has not expired (docs/17-DATA-INVARIANTS.md "Only the active
    /// non-expired revision can be accepted"). This does not reserve stock or create a deal —
    /// that is TASK-008.
    /// </summary>
    public void Accept(Guid acceptingMerchantProfileId, DateTime nowUtc)
    {
        RequireOpen("accepted");
        RequireResponder(acceptingMerchantProfileId, "accept this offer");
        RequireCurrentOfferActive(nowUtc, "accepted");

        Status = B2BNegotiationStatus.Accepted;
        Touch(nowUtc);
    }

    /// <summary>Rejects the current offer. Only the merchant it is addressed to can reject it. Terminal.</summary>
    public void Reject(Guid rejectingMerchantProfileId, DateTime nowUtc)
    {
        RequireOpen("rejected");
        RequireResponder(rejectingMerchantProfileId, "reject this offer");
        RequireCurrentOfferActive(nowUtc, "rejected");

        Status = B2BNegotiationStatus.Rejected;
        Touch(nowUtc);
    }

    /// <summary>
    /// A participating merchant withdraws from the negotiation before it is accepted. Either
    /// side may do this while the negotiation is open — a reversible default in the absence of
    /// a stricter rule (docs/13-OPEN-QUESTIONS.md). Terminal.
    /// </summary>
    public void Cancel(Guid cancellingMerchantProfileId, DateTime nowUtc)
    {
        RequireOpen("cancelled");

        if (!IsParticipant(cancellingMerchantProfileId))
        {
            throw new DomainException("Only a merchant in this negotiation can cancel it.");
        }

        RequireCurrentOfferActive(nowUtc, "cancelled");

        Status = B2BNegotiationStatus.Cancelled;
        Touch(nowUtc);
    }

    /// <summary>
    /// Closes an open negotiation whose current offer has lapsed
    /// (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §5 "Active revision expires -> Negotiation
    /// Expired"). Idempotent: returns <c>false</c> and changes nothing when the negotiation is
    /// not open or the offer has not expired.
    /// </summary>
    public bool ExpireIfLapsed(DateTime nowUtc)
    {
        if (!IsOpen || !CurrentOfferHasExpired(nowUtc))
        {
            return false;
        }

        Status = B2BNegotiationStatus.Expired;
        Touch(nowUtc);
        return true;
    }

    // ---- Internals ---------------------------------------------------------------

    private B2BOfferRevision AppendRevision(
        Guid proposingMerchantProfileId,
        int listingMinimumOrderQuantity,
        bool listingAllowsMixedVariantLots,
        ProposedOffer offer,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ValidateOffer(offer, listingMinimumOrderQuantity, listingAllowsMixedVariantLots, nowUtc);

        var revision = new B2BOfferRevision(
            CurrentRevisionNumber + 1,
            proposingMerchantProfileId,
            offer.UnitPrice,
            offer.Message,
            offer.OfferExpiresAtUtc,
            offer.Lines,
            nowUtc);

        _revisions.Add(revision);
        CurrentRevisionNumber = revision.RevisionNumber;
        Touch(nowUtc);
        return revision;
    }

    private static void ValidateOffer(
        ProposedOffer offer, int minimumOrderQuantity, bool allowMixedVariantLots, DateTime nowUtc)
    {
        if (offer.Lines.Count == 0)
        {
            throw new DomainException("An offer needs at least one variant line.");
        }

        if (offer.Lines.Select(l => l.ListingVariantId).Distinct().Count() != offer.Lines.Count)
        {
            throw new DomainException("An offer cannot list the same variant twice.");
        }

        if (offer.Lines.Any(l => l.Quantity <= 0))
        {
            throw new DomainException("Every offer line quantity must be greater than zero.");
        }

        if (offer.UnitPrice <= 0)
        {
            throw new DomainException("The proposed unit price must be greater than zero.");
        }

        if (!B2BOfferRevision.HasJodPrecision(offer.UnitPrice))
        {
            throw new DomainException("The proposed unit price must use no more than three decimal places for JOD.");
        }

        if (offer.OfferExpiresAtUtc <= nowUtc)
        {
            throw new DomainException("The offer's expiry must be in the future.");
        }

        // MOQ (docs/03-BUSINESS-RULES.md §11). When the seller allows mixed-lot purchase the
        // quantities across variants count together toward the listing minimum; otherwise each
        // variant is its own lot and must reach the minimum on its own.
        if (minimumOrderQuantity > 0)
        {
            if (allowMixedVariantLots)
            {
                var total = offer.Lines.Sum(l => l.Quantity);
                if (total < minimumOrderQuantity)
                {
                    throw new DomainException(
                        $"This listing's minimum wholesale order is {minimumOrderQuantity} units; the offer totals {total}.");
                }
            }
            else if (offer.Lines.Any(l => l.Quantity < minimumOrderQuantity))
            {
                throw new DomainException(
                    $"This listing's minimum wholesale order is {minimumOrderQuantity} units per variant.");
            }
        }
    }

    private void RequireOpen(string verb)
    {
        if (!IsOpen)
        {
            throw new DomainException($"A negotiation that is {Status} cannot be {verb}.");
        }
    }

    private void RequireResponder(Guid merchantProfileId, string action)
    {
        if (!IsParticipant(merchantProfileId))
        {
            throw new DomainException("Only a merchant in this negotiation can act on it.");
        }

        if (AwaitingResponseFrom != merchantProfileId)
        {
            throw new DomainException($"It is the other merchant's turn — you cannot {action} right now.");
        }
    }

    private void RequireCurrentOfferActive(DateTime nowUtc, string verb)
    {
        if (!CurrentOfferHasExpired(nowUtc))
        {
            return;
        }

        Status = B2BNegotiationStatus.Expired;
        Touch(nowUtc);
        throw new DomainException($"This offer has expired and can no longer be {verb}.");
    }

    private void Touch(DateTime nowUtc) => UpdatedAtUtc = nowUtc;
}

/// <summary>One requested variant line inside a proposed offer.</summary>
public sealed record ProposedOfferLine(Guid ListingVariantId, int Quantity);

/// <summary>
/// The terms of a single proposed offer or counter-offer, handed to
/// <see cref="B2BNegotiation"/> which turns it into an immutable <see cref="B2BOfferRevision"/>.
/// The service builds this from validated input and a server-resolved expiry timestamp —
/// no value here is trusted straight from the request (docs/08-SECURITY-AND-PRIVACY.md §6-7).
/// </summary>
public sealed record ProposedOffer(
    decimal UnitPrice,
    IReadOnlyList<ProposedOfferLine> Lines,
    string? Message,
    DateTime OfferExpiresAtUtc);
