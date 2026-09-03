using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;

namespace Faed.UnitTests;

/// <summary>
/// B2B negotiation aggregate rules (tasks/TASK-007-B2B-NEGOTIATION.md,
/// docs/03-BUSINESS-RULES.md §9, docs/05-USER-FLOWS-AND-STATE-MACHINES.md §5,
/// docs/17-DATA-INVARIANTS.md "B2B Negotiation"). The accepted deal, its stock reservation
/// and its separate expiry are TASK-008 and are deliberately not modelled here
/// (docs/adr/0004).
/// </summary>
public class B2BNegotiationTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Seller = Guid.NewGuid();
    private static readonly Guid Buyer = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();
    private static readonly Guid ListingId = Guid.NewGuid();
    private static readonly Guid VariantA = Guid.NewGuid();
    private static readonly Guid VariantB = Guid.NewGuid();

    private static ProposedOffer Offer(
        decimal unitPrice = 5m,
        DateTime? expiresAtUtc = null,
        string? message = null,
        params (Guid VariantId, int Quantity)[] lines)
    {
        var resolved = lines.Length == 0 ? [(VariantA, 10)] : lines;
        return new ProposedOffer(
            unitPrice,
            resolved.Select(l => new ProposedOfferLine(l.VariantId, l.Quantity)).ToList(),
            message,
            expiresAtUtc ?? Now.AddDays(3));
    }

    private static B2BNegotiation NewNegotiation(
        int minimumOrderQuantity = 10, bool allowMixedLots = false, ProposedOffer? offer = null) =>
        new(ListingId, Seller, Buyer, minimumOrderQuantity, allowMixedLots, offer ?? Offer(), Now);

    [Fact]
    public void NewNegotiation_StartsOpen_WithRevisionOneProposedByTheBuyingMerchant()
    {
        var negotiation = NewNegotiation();

        Assert.Equal(B2BNegotiationStatus.Open, negotiation.Status);
        Assert.Equal(1, negotiation.CurrentRevisionNumber);
        Assert.Single(negotiation.Revisions);
        Assert.Equal(Buyer, negotiation.CurrentRevision.ProposedByMerchantProfileId);
        Assert.Equal(Seller, negotiation.AwaitingResponseFrom);
    }

    [Fact]
    public void Ctor_WhenBuyingMerchantIsTheSellingMerchant_IsRejected()
    {
        Assert.Throws<DomainException>(() =>
            new B2BNegotiation(ListingId, Seller, Seller, 10, false, Offer(), Now));
    }

    [Fact]
    public void ProposedTotal_IsUnitPriceTimesSummedLineQuantities()
    {
        var negotiation = NewNegotiation(
            minimumOrderQuantity: 5, allowMixedLots: true,
            offer: Offer(unitPrice: 4m, lines: [(VariantA, 3), (VariantB, 7)]));

        Assert.Equal(10, negotiation.CurrentRevision.TotalQuantity);
        Assert.Equal(40m, negotiation.CurrentRevision.ProposedTotal);
    }

    [Fact]
    public void Counter_CreatesANewImmutableRevision_AndLeavesTheEarlierOnesUntouched()
    {
        var negotiation = NewNegotiation();
        var firstOfferExpiry = negotiation.CurrentRevision.OfferExpiresAtUtc;

        var counter = negotiation.Counter(
            Seller, 10, false, Offer(unitPrice: 6m, lines: [(VariantA, 12)]), Now.AddHours(1));

        Assert.Equal(2, negotiation.CurrentRevisionNumber);
        Assert.Equal(2, negotiation.Revisions.Count);
        Assert.Equal(2, counter.RevisionNumber);
        Assert.Equal(Seller, negotiation.CurrentRevision.ProposedByMerchantProfileId);
        Assert.Equal(Buyer, negotiation.AwaitingResponseFrom);

        var original = negotiation.Revisions.Single(r => r.RevisionNumber == 1);
        Assert.Equal(5m, original.ProposedUnitPrice);
        Assert.Equal(10, original.TotalQuantity);
        Assert.Equal(firstOfferExpiry, original.OfferExpiresAtUtc);
    }

    [Fact]
    public void Counter_BySameMerchantTwiceInARow_IsRejected()
    {
        var negotiation = NewNegotiation();

        // The buyer proposed revision 1; it is the seller's turn.
        Assert.Throws<DomainException>(() =>
            negotiation.Counter(Buyer, 10, false, Offer(lines: [(VariantA, 11)]), Now.AddHours(1)));
    }

    [Fact]
    public void Counter_ByANonParticipant_IsRejected()
    {
        var negotiation = NewNegotiation();

        Assert.Throws<DomainException>(() =>
            negotiation.Counter(Stranger, 10, false, Offer(lines: [(VariantA, 11)]), Now.AddHours(1)));
    }

    [Fact]
    public void Accept_ByTheMerchantTheOfferIsAddressedTo_MovesToAccepted()
    {
        var negotiation = NewNegotiation();

        negotiation.Accept(Seller, Now.AddHours(1));

        Assert.Equal(B2BNegotiationStatus.Accepted, negotiation.Status);
        Assert.Null(negotiation.AwaitingResponseFrom);
    }

    [Fact]
    public void Accept_ByTheMerchantWhoMadeTheCurrentOffer_IsRejected()
    {
        var negotiation = NewNegotiation();

        // The buyer cannot accept their own offer.
        Assert.Throws<DomainException>(() => negotiation.Accept(Buyer, Now.AddHours(1)));
        Assert.Equal(B2BNegotiationStatus.Open, negotiation.Status);
    }

    [Fact]
    public void Accept_WhenTheCurrentOfferHasExpired_IsRejected_AndExpiresTheNegotiation()
    {
        var negotiation = NewNegotiation(offer: Offer(expiresAtUtc: Now.AddHours(1)));

        var ex = Assert.Throws<DomainException>(() => negotiation.Accept(Seller, Now.AddHours(2)));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(B2BNegotiationStatus.Expired, negotiation.Status);
    }

    [Fact]
    public void CounterAndReject_WhenTheCurrentOfferHasExpired_AreRejectedWithoutCreatingARevision()
    {
        var countered = NewNegotiation(offer: Offer(expiresAtUtc: Now.AddHours(1)));
        var rejected = NewNegotiation(offer: Offer(expiresAtUtc: Now.AddHours(1)));

        Assert.Throws<DomainException>(() => countered.Counter(
            Seller, 10, false, Offer(lines: [(VariantA, 11)]), Now.AddHours(2)));
        Assert.Throws<DomainException>(() => rejected.Reject(Seller, Now.AddHours(2)));

        Assert.Equal(B2BNegotiationStatus.Expired, countered.Status);
        Assert.Single(countered.Revisions);
        Assert.Equal(B2BNegotiationStatus.Expired, rejected.Status);
    }

    [Fact]
    public void Cancel_WhenTheCurrentOfferHasExpired_IsRejected_AndExpiresTheNegotiation()
    {
        var negotiation = NewNegotiation(offer: Offer(expiresAtUtc: Now.AddHours(1)));

        Assert.Throws<DomainException>(() => negotiation.Cancel(Buyer, Now.AddHours(2)));

        Assert.Equal(B2BNegotiationStatus.Expired, negotiation.Status);
    }

    [Fact]
    public void Reject_ByTheAddressedMerchant_MovesToRejected()
    {
        var negotiation = NewNegotiation();

        negotiation.Reject(Seller, Now.AddHours(1));

        Assert.Equal(B2BNegotiationStatus.Rejected, negotiation.Status);
    }

    [Fact]
    public void Cancel_ByEitherParticipant_MovesToCancelled_ButNotByAStranger()
    {
        var negotiation = NewNegotiation();
        Assert.Throws<DomainException>(() => negotiation.Cancel(Stranger, Now.AddHours(1)));

        negotiation.Cancel(Buyer, Now.AddHours(1));
        Assert.Equal(B2BNegotiationStatus.Cancelled, negotiation.Status);
    }

    [Fact]
    public void AnyCommand_OnAClosedNegotiation_IsRejected()
    {
        var negotiation = NewNegotiation();
        negotiation.Accept(Seller, Now.AddHours(1));

        Assert.Throws<DomainException>(() =>
            negotiation.Counter(Buyer, 10, false, Offer(lines: [(VariantA, 12)]), Now.AddHours(2)));
        Assert.Throws<DomainException>(() => negotiation.Reject(Seller, Now.AddHours(2)));
        Assert.Throws<DomainException>(() => negotiation.Cancel(Buyer, Now.AddHours(2)));
    }

    [Fact]
    public void Moq_WhenMixedLotsAreNotAllowed_EveryLineMustReachTheMinimum()
    {
        Assert.Throws<DomainException>(() => NewNegotiation(
            minimumOrderQuantity: 10, allowMixedLots: false,
            offer: Offer(lines: [(VariantA, 10), (VariantB, 5)])));
    }

    [Fact]
    public void Moq_WhenMixedLotsAreAllowed_TheQuantitiesCountTogether()
    {
        // Total 12 across two variants clears a listing minimum of 10.
        var negotiation = NewNegotiation(
            minimumOrderQuantity: 10, allowMixedLots: true,
            offer: Offer(lines: [(VariantA, 6), (VariantB, 6)]));
        Assert.Equal(B2BNegotiationStatus.Open, negotiation.Status);

        // The same split is rejected when mixed lots are not allowed.
        Assert.Throws<DomainException>(() => NewNegotiation(
            minimumOrderQuantity: 10, allowMixedLots: false,
            offer: Offer(lines: [(VariantA, 6), (VariantB, 6)])));
    }

    [Fact]
    public void Offer_WithAnExpiryInThePast_IsRejected()
    {
        Assert.Throws<DomainException>(() =>
            NewNegotiation(offer: Offer(expiresAtUtc: Now.AddMinutes(-1))));
    }

    [Fact]
    public void Offer_ListingTheSameVariantTwice_IsRejected()
    {
        Assert.Throws<DomainException>(() => NewNegotiation(
            minimumOrderQuantity: 1, allowMixedLots: true,
            offer: Offer(lines: [(VariantA, 5), (VariantA, 5)])));
    }

    [Fact]
    public void Offer_WithANonPositiveUnitPrice_IsRejected()
    {
        Assert.Throws<DomainException>(() => NewNegotiation(offer: Offer(unitPrice: 0m)));
    }

    [Fact]
    public void Offer_WithMoreThanThreeJodDecimalPlaces_IsRejectedBeforeARevisionIsCreated()
    {
        Assert.Throws<DomainException>(() => NewNegotiation(offer: Offer(unitPrice: 4.1234m)));
    }

    [Fact]
    public void ExpireIfLapsed_OnlyClosesAnOpenNegotiationWhoseOfferHasLapsed_AndIsIdempotent()
    {
        var negotiation = NewNegotiation(offer: Offer(expiresAtUtc: Now.AddHours(1)));

        Assert.False(negotiation.ExpireIfLapsed(Now.AddMinutes(30)));
        Assert.Equal(B2BNegotiationStatus.Open, negotiation.Status);

        Assert.True(negotiation.ExpireIfLapsed(Now.AddHours(2)));
        Assert.Equal(B2BNegotiationStatus.Expired, negotiation.Status);

        Assert.False(negotiation.ExpireIfLapsed(Now.AddHours(3)));
    }

    [Fact]
    public void RevisionNumbers_StrictlyIncreaseAcrossACounterOfferChain()
    {
        var negotiation = NewNegotiation();
        negotiation.Counter(Seller, 10, false, Offer(lines: [(VariantA, 11)]), Now.AddHours(1));
        negotiation.Counter(Buyer, 10, false, Offer(lines: [(VariantA, 12)]), Now.AddHours(2));
        negotiation.Counter(Seller, 10, false, Offer(lines: [(VariantA, 13)]), Now.AddHours(3));

        Assert.Equal([1, 2, 3, 4], negotiation.Revisions.Select(r => r.RevisionNumber).ToArray());
        Assert.Equal(4, negotiation.CurrentRevisionNumber);
    }
}
