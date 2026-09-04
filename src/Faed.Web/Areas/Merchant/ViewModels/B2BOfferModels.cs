using System.ComponentModel.DataAnnotations;
using Faed.Web.Services.B2B;
using Faed.Web.Services.Common;

namespace Faed.Web.Areas.Merchant.ViewModels;

/// <summary>One editable variant row on the offer / counter-offer form.</summary>
public sealed class B2BOfferLineFormModel
{
    public Guid VariantId { get; set; }

    /// <summary>Shown next to the input; not posted back for authority (the service re-resolves it).</summary>
    public string Combination { get; set; } = string.Empty;

    public int AvailableQuantity { get; set; }

    [Range(0, 100_000, ErrorMessage = "Enter a quantity between 0 and 100000.")]
    public int Quantity { get; set; }
}

/// <summary>The "make an offer" / counter-offer input model.</summary>
public sealed class B2BOfferFormModel
{
    public string ListingSlug { get; set; } = string.Empty;

    [Display(Name = "Proposed unit price (JOD)")]
    [Range(0.001, 1_000_000, ErrorMessage = "Enter a unit price greater than zero.")]
    public decimal ProposedUnitPrice { get; set; }

    [Display(Name = "Offer valid for (days)")]
    [Range(1, 30, ErrorMessage = "Choose an offer validity between 1 and 30 days.")]
    public int ValidityDays { get; set; } = 3;

    [Display(Name = "Message (optional)")]
    [StringLength(2000)]
    public string? Message { get; set; }

    public List<B2BOfferLineFormModel> Lines { get; set; } = [];

    public IReadOnlyList<B2BOfferLineInput> ToLineInputs() =>
        Lines.Where(l => l.Quantity > 0)
            .Select(l => new B2BOfferLineInput(l.VariantId, l.Quantity))
            .ToList();
}

public sealed class B2BOfferCreatePageModel
{
    public required OfferListingView Listing { get; init; }

    public required B2BOfferFormModel Form { get; init; }
}

public sealed class B2BNegotiationListPageModel
{
    public required B2BNegotiationFilter Filter { get; init; }

    public required PagedResult<B2BNegotiationSummaryView> Negotiations { get; init; }

    public int AwaitingMeCount { get; init; }
}

public sealed class B2BNegotiationDetailPageModel
{
    public required B2BNegotiationDetailView Negotiation { get; init; }

    public required B2BOfferFormModel CounterForm { get; init; }
}
